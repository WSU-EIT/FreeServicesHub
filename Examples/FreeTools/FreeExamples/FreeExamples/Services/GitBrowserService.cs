using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FreeExamples.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FreeExamples.Server.Services;

/// <summary>
/// Service that browses public git repositories entirely via the GitHub REST API.
/// Nothing is ever cloned or written to disk. Repository trees and file contents
/// are fetched over HTTP and cached in memory as C# objects.
/// Emits real-time progress via SignalR so the client can show each step.
/// </summary>
public class GitBrowserService : IDisposable
{
    /// <summary>
    /// Represents one node in the in-memory file tree (either a directory or a file).
    /// </summary>
    private class MemoryNode
    {
        public string Name { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public string Extension { get; set; } = "";
        public bool IsBinary { get; set; }
        public string? Content { get; set; }

        /// <summary>SHA blob hash for lazy-loading file content from the API.</summary>
        public string? Sha { get; set; }

        /// <summary>Children keyed by name (only populated for directories).</summary>
        public Dictionary<string, MemoryNode>? Children { get; set; }
    }

    private readonly ConcurrentDictionary<string, MemoryNode> _repos = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly IHubContext<freeexamplesHub, IsrHub> _signalR;
    private readonly HttpClient _http;
    private bool _disposed;
    private DateTime _lastProgressTime = DateTime.MinValue;

    private const long MaxFileSize = 512 * 1024; // 512 KB
    private const int MinProgressGapMs = 400; // minimum ms between progress messages

    public GitBrowserService(IHubContext<freeexamplesHub, IsrHub> signalR)
    {
        _signalR = signalR;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FreeExamples-GitBrowser", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Sends a progress update to all clients via SignalR.
    /// Waits at least MinProgressGapMs between messages so humans can read them.
    /// </summary>
    private async Task SendProgress(string message)
    {
        try {
            var elapsed = (DateTime.UtcNow - _lastProgressTime).TotalMilliseconds;
            if (elapsed < MinProgressGapMs)
                await Task.Delay((int)(MinProgressGapMs - elapsed));

            _lastProgressTime = DateTime.UtcNow;

            await _signalR.Clients.All.SignalRUpdate(new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.GitCloneProgress,
                Message = message,
            });
        } catch {
            // Best-effort
        }
    }

    /// <summary>
    /// Parses a GitHub URL into (owner, repo). Supports:
    ///   https://github.com/owner/repo
    ///   https://github.com/owner/repo.git
    ///   https://github.com/owner/repo/tree/branch/path
    /// </summary>
    private static (string Owner, string Repo)? ParseGitHubUrl(string url)
    {
        url = url.Trim().TrimEnd('/');

        // Remove .git suffix
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            url = url[..^4];

        if (!url.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return null;

        // Extract path after github.com
        var uri = new Uri(url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : "https://" + url);
        var segments = uri.AbsolutePath.Trim('/').Split('/');

        if (segments.Length < 2)
            return null;

        return (segments[0], segments[1]);
    }

    /// <summary>
    /// Ensures a repo tree is loaded into memory via the GitHub API.
    /// Uses the Git Trees API with recursive=1 to get the entire tree in one call.
    /// </summary>
    private async Task<MemoryNode> EnsureLoadedAsync(string repoUrl)
    {
        if (_repos.TryGetValue(repoUrl, out var cached))
        {
            await SendProgress("Repository already cached in memory.");
            return cached;
        }

        await _loadLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_repos.TryGetValue(repoUrl, out cached))
            {
                await SendProgress("Repository already cached in memory.");
                return cached;
            }

            var parsed = ParseGitHubUrl(repoUrl);
            if (parsed == null)
            {
                await SendProgress("Only GitHub repositories are supported. URL must contain github.com/owner/repo.");
                throw new InvalidOperationException("Only GitHub repositories are supported.");
            }

            var (owner, repo) = parsed.Value;
            await SendProgress($"Fetching repository info for {owner}/{repo}...");

            // Step 1: Get default branch
            var repoInfoUrl = $"https://api.github.com/repos/{owner}/{repo}";
            var repoResponse = await _http.GetAsync(repoInfoUrl);

            if (!repoResponse.IsSuccessStatusCode)
            {
                var status = repoResponse.StatusCode;
                await SendProgress($"Failed to access repository ({status}). Is it a valid public GitHub repo?");
                throw new InvalidOperationException($"GitHub API returned {status} for {repoInfoUrl}");
            }

            var repoJson = await repoResponse.Content.ReadAsStringAsync();
            using var repoDoc = JsonDocument.Parse(repoJson);
            var defaultBranch = repoDoc.RootElement.GetProperty("default_branch").GetString() ?? "main";

            await SendProgress($"Default branch: {defaultBranch}. Fetching full file tree...");

            // Step 2: Get the full tree recursively (single API call)
            var treeUrl = $"https://api.github.com/repos/{owner}/{repo}/git/trees/{defaultBranch}?recursive=1";
            var treeResponse = await _http.GetAsync(treeUrl);

            if (!treeResponse.IsSuccessStatusCode)
            {
                await SendProgress($"Failed to fetch tree ({treeResponse.StatusCode}).");
                throw new InvalidOperationException($"GitHub API returned {treeResponse.StatusCode} for tree request");
            }

            var treeJson = await treeResponse.Content.ReadAsStringAsync();
            using var treeDoc = JsonDocument.Parse(treeJson);

            var truncated = treeDoc.RootElement.TryGetProperty("truncated", out var truncProp) && truncProp.GetBoolean();
            var treeArray = treeDoc.RootElement.GetProperty("tree");

            // Step 3: Build in-memory tree from the flat list
            var root = new MemoryNode
            {
                Name = "",
                RelativePath = "",
                IsDirectory = true,
                Children = new(StringComparer.OrdinalIgnoreCase),
            };

            int fileCount = 0;
            int dirCount = 0;
            long totalSize = 0;

            foreach (var item in treeArray.EnumerateArray())
            {
                var path = item.GetProperty("path").GetString() ?? "";
                var type = item.GetProperty("type").GetString() ?? "";
                var sha = item.TryGetProperty("sha", out var shaProp) ? shaProp.GetString() : null;
                var size = item.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;

                // Skip dot-directories (like .github, .gitignore is fine as a file)
                var topSegment = path.Split('/')[0];
                if (type == "tree" && topSegment.StartsWith('.'))
                    continue;
                if (type == "blob" && path.Contains('/'))
                {
                    var parentSegment = path.Split('/')[0];
                    if (parentSegment.StartsWith('.'))
                        continue;
                }

                if (type == "tree")
                {
                    EnsureDirectoryPath(root, path);
                    dirCount++;
                }
                else if (type == "blob")
                {
                    var name = Path.GetFileName(path);
                    var ext = Path.GetExtension(name).ToLowerInvariant();
                    var parentNode = EnsureDirectoryPath(root, Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "");

                    var fileNode = new MemoryNode
                    {
                        Name = name,
                        RelativePath = path,
                        IsDirectory = false,
                        Size = size,
                        Extension = ext,
                        Sha = sha,
                    };

                    parentNode.Children![name] = fileNode;
                    fileCount++;
                    totalSize += size;
                }
            }

            await SendProgress($"Tree loaded: {fileCount:N0} files, {dirCount:N0} folders ({FormatBytes(totalSize)}) in memory.");

            if (truncated)
                await SendProgress("Note: Repository is very large. Some files may be missing from the tree.");

            await SendProgress("Done. Serving entirely from memory (no files on disk).");

            _repos[repoUrl] = root;
            return root;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Ensures all directory nodes along a path exist and returns the deepest one.
    /// </summary>
    private static MemoryNode EnsureDirectoryPath(MemoryNode root, string dirPath)
    {
        if (string.IsNullOrEmpty(dirPath))
            return root;

        var current = root;
        var runningPath = "";

        foreach (var segment in dirPath.Split('/'))
        {
            if (string.IsNullOrEmpty(segment)) continue;

            runningPath = string.IsNullOrEmpty(runningPath) ? segment : runningPath + "/" + segment;

            current.Children ??= new(StringComparer.OrdinalIgnoreCase);

            if (!current.Children.TryGetValue(segment, out var child))
            {
                child = new MemoryNode
                {
                    Name = segment,
                    RelativePath = runningPath,
                    IsDirectory = true,
                    Children = new(StringComparer.OrdinalIgnoreCase),
                };
                current.Children[segment] = child;
            }

            current = child;
        }

        return current;
    }

    /// <summary>
    /// Navigates the in-memory tree to find a node at the given path.
    /// </summary>
    private static MemoryNode? FindNode(MemoryNode root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return root;

        var current = root;
        foreach (var segment in path.Split('/'))
        {
            if (string.IsNullOrEmpty(segment)) continue;
            if (current.Children == null || !current.Children.TryGetValue(segment, out var child))
                return null;
            current = child;
        }
        return current;
    }

    /// <summary>
    /// Lazy-loads a file's content from the GitHub Blobs API when first accessed.
    /// </summary>
    private async Task EnsureFileContentLoaded(MemoryNode node, string repoUrl)
    {
        // Already loaded
        if (node.Content != null)
            return;

        if (node.Size > MaxFileSize)
        {
            node.IsBinary = true;
            node.Content = $"[File too large to display: {node.Size:N0} bytes]";
            return;
        }

        if (string.IsNullOrEmpty(node.Sha))
        {
            node.Content = "[Unable to load: no SHA reference]";
            return;
        }

        var parsed = ParseGitHubUrl(repoUrl);
        if (parsed == null)
        {
            node.Content = "[Unable to load: invalid repo URL]";
            return;
        }

        var (owner, repo) = parsed.Value;
        var blobUrl = $"https://api.github.com/repos/{owner}/{repo}/git/blobs/{node.Sha}";

        try
        {
            var response = await _http.GetAsync(blobUrl);
            if (!response.IsSuccessStatusCode)
            {
                node.Content = $"[Failed to fetch file: {response.StatusCode}]";
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var encoding = doc.RootElement.GetProperty("encoding").GetString();
            var contentRaw = doc.RootElement.GetProperty("content").GetString() ?? "";

            if (encoding == "base64")
            {
                var bytes = Convert.FromBase64String(contentRaw.Replace("\n", ""));

                // Binary detection: check for null bytes in the first 8KB
                var checkLen = Math.Min(bytes.Length, 8192);
                bool isBinary = false;
                for (int i = 0; i < checkLen; i++)
                {
                    if (bytes[i] == 0) { isBinary = true; break; }
                }

                if (isBinary)
                {
                    node.IsBinary = true;
                    node.Content = $"[Binary file: {bytes.Length:N0} bytes]";
                }
                else
                {
                    node.Content = Encoding.UTF8.GetString(bytes);
                }
            }
            else
            {
                // utf-8 encoding returned directly
                node.Content = contentRaw;
            }
        }
        catch (Exception ex)
        {
            node.Content = $"[Error loading file: {ex.Message}]";
        }
    }

    /// <summary>
    /// Lists entries (files/folders) at a given path within the repo from memory.
    /// </summary>
    public async Task<List<DataObjects.GitRepoEntry>> BrowseAsync(string repoUrl, string? path)
    {
        var root = await EnsureLoadedAsync(repoUrl);
        var node = FindNode(root, path);

        if (node == null || !node.IsDirectory || node.Children == null)
            return new List<DataObjects.GitRepoEntry>();

        var entries = new List<DataObjects.GitRepoEntry>();

        // Directories first, then files
        foreach (var child in node.Children.Values.Where(c => c.IsDirectory).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(new DataObjects.GitRepoEntry
            {
                Name = child.Name,
                Path = child.RelativePath,
                IsDirectory = true,
            });
        }

        foreach (var child in node.Children.Values.Where(c => !c.IsDirectory).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(new DataObjects.GitRepoEntry
            {
                Name = child.Name,
                Path = child.RelativePath,
                IsDirectory = false,
                Size = child.Size,
                Extension = child.Extension,
            });
        }

        return entries;
    }

    /// <summary>
    /// Reads the content of a single file. Lazy-loads from the GitHub Blobs API on first access.
    /// </summary>
    public async Task<DataObjects.GitFileContent> GetFileAsync(string repoUrl, string filePath)
    {
        var root = await EnsureLoadedAsync(repoUrl);
        var node = FindNode(root, filePath);

        var result = new DataObjects.GitFileContent
        {
            Path = filePath,
            Name = Path.GetFileName(filePath),
            Extension = Path.GetExtension(filePath).ToLowerInvariant(),
        };

        if (node == null || node.IsDirectory)
        {
            result.Content = "File not found.";
            return result;
        }

        // Lazy-load content from API on first access
        await EnsureFileContentLoaded(node, repoUrl);

        result.Size = node.Size;
        result.IsBinary = node.IsBinary;
        result.Content = node.Content ?? "";
        return result;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _repos.Clear();
        _loadLock.Dispose();
        _http.Dispose();
    }
}

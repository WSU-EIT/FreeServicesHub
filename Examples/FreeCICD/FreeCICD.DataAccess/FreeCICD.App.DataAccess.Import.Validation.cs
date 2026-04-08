using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace FreeCICD;

// Import Validation: URL validation, conflict detection, and name suggestions

public partial class DataAccess
{
    /// <summary>
    /// Validates a public Git repository URL and retrieves metadata.
    /// For GitHub: Uses the GitHub API to get full repository details.
    /// For other sources: Extracts information from URL pattern.
    /// </summary>
    public async Task<DataObjects.PublicGitRepoInfo> ValidatePublicGitRepoAsync(string url)
    {
        var result = new DataObjects.PublicGitRepoInfo { Url = url };

        try {
            // Basic URL validation
            if (string.IsNullOrWhiteSpace(url)) {
                result.IsValid = false;
                result.ErrorMessage = "Please enter a Git repository URL.";
                return result;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                result.IsValid = false;
                result.ErrorMessage = "Please enter a valid URL.";
                return result;
            }

            // Detect source and parse URL
            var host = uri.Host.ToLowerInvariant();
            
            if (host.Contains("github.com")) {
                return await ValidateGitHubRepoAsync(url, uri);
            } else if (host.Contains("gitlab.com")) {
                return ParseGitLabUrl(url, uri);
            } else if (host.Contains("bitbucket.org")) {
                return ParseBitbucketUrl(url, uri);
            } else {
                // Generic Git URL - extract name from path
                return ParseGenericGitUrl(url, uri);
            }
        } catch (Exception ex) {
            result.IsValid = false;
            result.ErrorMessage = $"Error validating repository: {ex.Message}";
            return result;
        }
    }

    private async Task<DataObjects.PublicGitRepoInfo> ValidateGitHubRepoAsync(string url, Uri uri)
    {
        var result = new DataObjects.PublicGitRepoInfo { Url = url, Source = "GitHub" };

        try {
            // Parse GitHub URL: https://github.com/{owner}/{repo}
            var pathParts = uri.AbsolutePath.Trim('/').Split('/');
            if (pathParts.Length < 2) {
                result.IsValid = false;
                result.ErrorMessage = "Invalid GitHub URL format. Expected: https://github.com/{owner}/{repo}";
                return result;
            }

            var owner = pathParts[0];
            var repo = pathParts[1].Replace(".git", "");

            // Call GitHub API for full metadata
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "FreeCICD");
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}";
            var response = await httpClient.GetAsync(apiUrl);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
                result.IsValid = false;
                result.ErrorMessage = "Repository not found or is private.";
                return result;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) {
                // Check for rate limiting
                if (response.Headers.Contains("X-RateLimit-Remaining")) {
                    var remaining = response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
                    if (remaining == "0") {
                        var resetTime = response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault();
                        if (long.TryParse(resetTime, out var resetUnix)) {
                            var resetDateTime = DateTimeOffset.FromUnixTimeSeconds(resetUnix).LocalDateTime;
                            var minutesRemaining = (int)Math.Ceiling((resetDateTime - DateTime.Now).TotalMinutes);
                            result.IsValid = false;
                            result.ErrorMessage = $"GitHub rate limit exceeded. Try again in {minutesRemaining} minutes.";
                            return result;
                        }
                    }
                }
                result.IsValid = false;
                result.ErrorMessage = "Access denied by GitHub API.";
                return result;
            }

            if (!response.IsSuccessStatusCode) {
                result.IsValid = false;
                result.ErrorMessage = $"GitHub API error: {response.StatusCode}";
                return result;
            }

            var json = await response.Content.ReadAsStringAsync();
            var repoData = System.Text.Json.JsonDocument.Parse(json);
            var root = repoData.RootElement;

            result.Name = root.GetProperty("name").GetString() ?? repo;
            result.Owner = root.GetProperty("owner").GetProperty("login").GetString() ?? owner;
            result.CloneUrl = root.GetProperty("clone_url").GetString() ?? $"https://github.com/{owner}/{repo}.git";
            result.DefaultBranch = root.GetProperty("default_branch").GetString() ?? "main";
            result.Description = root.TryGetProperty("description", out var desc) && desc.ValueKind != System.Text.Json.JsonValueKind.Null 
                ? desc.GetString() 
                : null;
            result.SizeKB = root.TryGetProperty("size", out var size) ? size.GetInt64() : null;
            result.IsValid = true;

            return result;
        } catch (TaskCanceledException) {
            result.IsValid = false;
            result.ErrorMessage = "Request timed out. Please check your connection and try again.";
            return result;
        } catch (HttpRequestException ex) {
            result.IsValid = false;
            result.ErrorMessage = $"Network error: {ex.Message}";
            return result;
        }
    }

    private DataObjects.PublicGitRepoInfo ParseGitLabUrl(string url, Uri uri)
    {
        // Parse GitLab URL: https://gitlab.com/{owner}/{repo}
        var pathParts = uri.AbsolutePath.Trim('/').Split('/');
        if (pathParts.Length < 2) {
            return new DataObjects.PublicGitRepoInfo {
                Url = url,
                Source = "GitLab",
                IsValid = false,
                ErrorMessage = "Invalid GitLab URL format."
            };
        }

        var owner = pathParts[0];
        var repo = pathParts[1].Replace(".git", "");

        return new DataObjects.PublicGitRepoInfo {
            Url = url,
            CloneUrl = url.EndsWith(".git") ? url : $"{url}.git",
            Name = repo,
            Owner = owner,
            Source = "GitLab",
            DefaultBranch = "main",
            IsValid = true
        };
    }

    private DataObjects.PublicGitRepoInfo ParseBitbucketUrl(string url, Uri uri)
    {
        // Parse Bitbucket URL: https://bitbucket.org/{owner}/{repo}
        var pathParts = uri.AbsolutePath.Trim('/').Split('/');
        if (pathParts.Length < 2) {
            return new DataObjects.PublicGitRepoInfo {
                Url = url,
                Source = "Bitbucket",
                IsValid = false,
                ErrorMessage = "Invalid Bitbucket URL format."
            };
        }

        var owner = pathParts[0];
        var repo = pathParts[1].Replace(".git", "");

        return new DataObjects.PublicGitRepoInfo {
            Url = url,
            CloneUrl = url.EndsWith(".git") ? url : $"{url}.git",
            Name = repo,
            Owner = owner,
            Source = "Bitbucket",
            DefaultBranch = "main",
            IsValid = true
        };
    }

    private DataObjects.PublicGitRepoInfo ParseGenericGitUrl(string url, Uri uri)
    {
        // Try to extract repo name from the URL path
        var pathParts = uri.AbsolutePath.Trim('/').Split('/');
        var lastPart = pathParts.LastOrDefault()?.Replace(".git", "") ?? "repository";

        return new DataObjects.PublicGitRepoInfo {
            Url = url,
            CloneUrl = url.EndsWith(".git") ? url : $"{url}.git",
            Name = lastPart,
            Owner = pathParts.Length > 1 ? pathParts[^2] : "unknown",
            Source = "Git",
            DefaultBranch = "main",
            IsValid = true
        };
    }

    /// <summary>
    /// Checks for conflicts before starting an import operation.
    /// Detects: project name conflicts, repo name conflicts, duplicate imports.
    /// </summary>
    public async Task<DataObjects.ImportConflictInfo> CheckImportConflictsAsync(
        string pat, string orgName, string? targetProjectId, string? newProjectName, string repoName, string sourceUrl)
    {
        var result = new DataObjects.ImportConflictInfo();

        try {
            using var connection = CreateConnection(pat, orgName);
            var projectClient = connection.GetClient<ProjectHttpClient>();
            var gitClient = connection.GetClient<GitHttpClient>();

            // Scenario 1: User wants to import into an EXISTING project
            if (!string.IsNullOrWhiteSpace(targetProjectId)) {
                // Only check for repo name conflicts in the target project
                try {
                    var existingRepos = await gitClient.GetRepositoriesAsync(targetProjectId);
                    var existingRepo = existingRepos.FirstOrDefault(r => 
                        string.Equals(r.Name, repoName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existingRepo != null) {
                        result.HasRepoConflict = true;
                        result.ExistingRepoId = existingRepo.Id.ToString();
                        result.ExistingRepoName = existingRepo.Name;
                        
                        // Try to get URL - Links may be null on GetRepositoriesAsync
                        if (existingRepo.Links?.Links != null && existingRepo.Links.Links.ContainsKey("web")) {
                            try {
                                dynamic webLink = existingRepo.Links.Links["web"];
                                result.ExistingRepoUrl = webLink.Href;
                            } catch {
                                // Ignore URL extraction errors
                            }
                        }
                        // Fallback to WebUrl or RemoteUrl if Links not available
                        if (string.IsNullOrWhiteSpace(result.ExistingRepoUrl)) {
                            result.ExistingRepoUrl = existingRepo.WebUrl ?? existingRepo.RemoteUrl;
                        }
                        
                        // Generate suggested alternative names
                        result.SuggestedRepoNames = GenerateSuggestedNames(repoName, 
                            existingRepos.Select(r => r.Name).ToList());
                    }
                    
                    // Also check for duplicate import (same source URL name already exists)
                    var normalizedSourceName = ExtractRepoNameFromUrl(sourceUrl);
                    if (!string.IsNullOrWhiteSpace(normalizedSourceName) && 
                        !string.Equals(normalizedSourceName, repoName, StringComparison.OrdinalIgnoreCase)) {
                        // User specified a different repo name, check if source name also exists
                        var sourceNameRepo = existingRepos.FirstOrDefault(r => 
                            string.Equals(r.Name, normalizedSourceName, StringComparison.OrdinalIgnoreCase));
                        if (sourceNameRepo != null) {
                            result.IsDuplicateImport = true;
                            if (sourceNameRepo.Links?.Links != null && sourceNameRepo.Links.Links.ContainsKey("web")) {
                                try {
                                    dynamic webLink = sourceNameRepo.Links.Links["web"];
                                    result.PreviousImportRepoUrl = webLink.Href;
                                } catch { }
                            }
                            if (string.IsNullOrWhiteSpace(result.PreviousImportRepoUrl)) {
                                result.PreviousImportRepoUrl = sourceNameRepo.WebUrl ?? sourceNameRepo.RemoteUrl;
                            }
                        }
                    }
                } catch {
                    // Ignore errors checking repos - let actual import handle it
                }
                
                return result;
            }

            // Scenario 2: User wants to create a NEW project
            if (!string.IsNullOrWhiteSpace(newProjectName)) {
                try {
                    var existingProjects = await projectClient.GetProjects();
                    var existingProject = existingProjects.FirstOrDefault(p => 
                        string.Equals(p.Name, newProjectName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existingProject != null) {
                        result.HasProjectConflict = true;
                        result.ExistingProjectId = existingProject.Id.ToString();
                        result.ExistingProjectName = existingProject.Name;
                        
                        // Generate suggested alternative project names
                        result.SuggestedProjectNames = GenerateSuggestedNames(newProjectName, 
                            existingProjects.Select(p => p.Name).ToList());
                        
                        // Since project exists, also check if repo would conflict in that project
                        try {
                            var existingRepos = await gitClient.GetRepositoriesAsync(existingProject.Id.ToString());
                            var existingRepo = existingRepos.FirstOrDefault(r => 
                                string.Equals(r.Name, repoName, StringComparison.OrdinalIgnoreCase));
                            
                            if (existingRepo != null) {
                                result.HasRepoConflict = true;
                                result.ExistingRepoId = existingRepo.Id.ToString();
                                result.ExistingRepoName = existingRepo.Name;
                                
                                if (existingRepo.Links?.Links != null && existingRepo.Links.Links.ContainsKey("web")) {
                                    try {
                                        dynamic webLink = existingRepo.Links.Links["web"];
                                        result.ExistingRepoUrl = webLink.Href;
                                    } catch { }
                                }
                                if (string.IsNullOrWhiteSpace(result.ExistingRepoUrl)) {
                                    result.ExistingRepoUrl = existingRepo.WebUrl ?? existingRepo.RemoteUrl;
                                }
                                
                                result.SuggestedRepoNames = GenerateSuggestedNames(repoName, 
                                    existingRepos.Select(r => r.Name).ToList());
                            }
                        } catch {
                            // Ignore errors checking repos in existing project
                        }
                    }
                    // If project doesn't exist, no conflicts - it will be created fresh
                } catch (InvalidOperationException) {
                    throw; // Re-throw our own exceptions
                } catch {
                    // Ignore errors checking projects - let actual import handle it
                }
            }

        } catch (Exception) {
            // Return empty result on error - let the actual import handle auth errors etc.
        }

        return result;
    }

    private List<string> GenerateSuggestedNames(string baseName, List<string> existingNames)
    {
        var suggestions = new List<string>();
        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        
        // Suggest with source indicator
        var withGithub = $"{baseName}-github";
        if (!existingSet.Contains(withGithub)) suggestions.Add(withGithub);
        
        // Suggest with "imported" suffix
        var withImported = $"{baseName}-imported";
        if (!existingSet.Contains(withImported)) suggestions.Add(withImported);
        
        // Suggest with date
        var withDate = $"{baseName}-{DateTime.Now:yyyy-MM-dd}";
        if (!existingSet.Contains(withDate)) suggestions.Add(withDate);
        
        // Suggest with incrementing number
        for (int i = 2; i <= 5 && suggestions.Count < 4; i++) {
            var withNumber = $"{baseName}-{i}";
            if (!existingSet.Contains(withNumber)) suggestions.Add(withNumber);
        }
        
        return suggestions.Take(4).ToList();
    }

    private string ExtractRepoNameFromUrl(string url)
    {
        try {
            var uri = new Uri(url);
            var pathParts = uri.AbsolutePath.Trim('/').Split('/');
            return pathParts.LastOrDefault()?.Replace(".git", "") ?? "";
        } catch {
            return "";
        }
    }
}

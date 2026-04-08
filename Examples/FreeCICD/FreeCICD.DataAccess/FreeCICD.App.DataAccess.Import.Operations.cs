using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using System.IO.Compression;

namespace FreeCICD;

// Import Operations: Project/repo creation, import execution, and status tracking

public partial class DataAccess
{
    /// <summary>
    /// Creates a new Azure DevOps project with Git source control.
    /// Polls until the project is fully created (wellFormed state).
    /// </summary>
    public async Task<DataObjects.DevopsProjectInfo> CreateDevOpsProjectAsync(string pat, string orgName, string projectName, string? description = null, string? connectionId = null)
    {
        try {
            using var connection = CreateConnection(pat, orgName);
            var projectClient = connection.GetClient<ProjectHttpClient>();

            // Create new project with Git source control
            var projectToCreate = new TeamProject {
                Name = projectName,
                Description = description ?? $"Imported from public repository",
                Capabilities = new Dictionary<string, Dictionary<string, string>> {
                    ["versioncontrol"] = new Dictionary<string, string> { ["sourceControlType"] = "Git" },
                    ["processTemplate"] = new Dictionary<string, string> { ["templateTypeId"] = "6b724908-ef14-45cf-84f8-768b5384da45" } // Agile
                }
            };

            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                    ConnectionId = connectionId,
                    ItemId = Guid.NewGuid(),
                    Message = $"Creating project '{projectName}'..."
                });
            }

            var operationRef = await projectClient.QueueCreateProject(projectToCreate);

            // Poll for project creation completion (max 60 seconds)
            var maxWait = TimeSpan.FromSeconds(60);
            var pollInterval = TimeSpan.FromSeconds(2);
            var elapsed = TimeSpan.Zero;

            while (elapsed < maxWait) {
                await Task.Delay(pollInterval);
                elapsed += pollInterval;

                try {
                    var project = await projectClient.GetProject(projectName);
                    if (project != null && project.State == ProjectState.WellFormed) {
                        string resourceUrl = string.Empty;
                        if (project.Links?.Links != null && project.Links.Links.ContainsKey("web")) {
                            dynamic webLink = project.Links.Links["web"];
                            resourceUrl = webLink.Href;
                        }
                        return new DataObjects.DevopsProjectInfo {
                            ProjectId = project.Id.ToString(),
                            ProjectName = project.Name,
                            CreationDate = project.LastUpdateTime,
                            ResourceUrl = resourceUrl
                        };
                    }
                } catch {
                    // Project not ready yet, continue polling
                }
            }

            throw new TimeoutException("Project creation timed out. The project may still be creating in Azure DevOps.");

        } catch (Exception) {
            throw;
        }
    }

    /// <summary>
    /// Creates a new Git repository in an Azure DevOps project.
    /// </summary>
    public async Task<DataObjects.DevopsGitRepoInfo> CreateDevOpsRepoAsync(string pat, string orgName, string projectId, string repoName, string? connectionId = null)
    {
        try {
            using var connection = CreateConnection(pat, orgName);
            var gitClient = connection.GetClient<GitHttpClient>();

            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                    ConnectionId = connectionId,
                    ItemId = Guid.NewGuid(),
                    Message = $"Creating repository '{repoName}'..."
                });
            }

            var newRepo = new GitRepositoryCreateOptions {
                Name = repoName,
                ProjectReference = new TeamProjectReference { Id = new Guid(projectId) }
            };

            var createdRepo = await gitClient.CreateRepositoryAsync(newRepo);

            return new DataObjects.DevopsGitRepoInfo {
                RepoId = createdRepo.Id.ToString(),
                RepoName = createdRepo.Name,
                ResourceUrl = createdRepo.WebUrl ?? createdRepo.RemoteUrl ?? string.Empty
            };

        } catch (Exception) {
            throw;
        }
    }

    /// <summary>
    /// Imports a public Git repository into Azure DevOps.
    /// Simplified flow:
    /// 1. Check if project exists → use existing or create new
    /// 2. Check if repo exists → use existing or create new  
    /// 3. Import code as a new branch (always flat snapshot, no history)
    /// </summary>
    public async Task<DataObjects.ImportPublicRepoResponse> ImportPublicRepoAsync(string pat, string orgName, DataObjects.ImportPublicRepoRequest request, string? connectionId = null)
    {
        var result = new DataObjects.ImportPublicRepoResponse();

        try {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(request.NewProjectName)) {
                result.Success = false;
                result.ErrorMessage = "Project name is required.";
                return result;
            }

            // For ZipUpload, we need the uploaded file
            if (request.Method == DataObjects.ImportMethod.ZipUpload) {
                if (!request.UploadedFileId.HasValue) {
                    result.Success = false;
                    result.ErrorMessage = "UploadedFileId is required for ZipUpload method.";
                    return result;
                }
            } else {
                // Validate source URL for Git methods
                if (string.IsNullOrWhiteSpace(request.SourceUrl)) {
                    result.Success = false;
                    result.ErrorMessage = "Source URL is required.";
                    return result;
                }
            }

            // Validate the source URL (if provided)
            DataObjects.PublicGitRepoInfo? repoInfo = null;
            if (!string.IsNullOrWhiteSpace(request.SourceUrl)) {
                repoInfo = await ValidatePublicGitRepoAsync(request.SourceUrl);
                if (!repoInfo.IsValid) {
                    result.Success = false;
                    result.ErrorMessage = repoInfo.ErrorMessage;
                    return result;
                }
            }

            using var connection = CreateConnection(pat, orgName);
            var gitClient = connection.GetClient<GitHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();

            // === Step 1: Get or create project ===
            string projectId;
            string projectName = request.NewProjectName;
            
            var existingProjects = await projectClient.GetProjects();
            var existingProject = existingProjects.FirstOrDefault(p => 
                string.Equals(p.Name, request.NewProjectName, StringComparison.OrdinalIgnoreCase));
            
            if (existingProject != null) {
                // Project exists - use it
                projectId = existingProject.Id.ToString();
                projectName = existingProject.Name;
                result.ProjectExisted = true;
                
                if (!string.IsNullOrWhiteSpace(connectionId)) {
                    await SignalRUpdate(new DataObjects.SignalRUpdate {
                        UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                        ConnectionId = connectionId,
                        ItemId = Guid.NewGuid(),
                        Message = $"Using existing project '{projectName}'..."
                    });
                }
            } else {
                // Create new project
                var projectResult = await CreateDevOpsProjectAsync(pat, orgName, request.NewProjectName, repoInfo?.Description, connectionId);
                projectId = projectResult.ProjectId!;
                projectName = projectResult.ProjectName!;
                result.ProjectExisted = false;
            }

            result.ProjectId = projectId;
            result.ProjectName = projectName;

            // === Step 2: Get or create repository ===
            // Determine target repo name:
            // - If explicitly specified by user, use that
            // - If project was just created (new project), default to project name (Azure DevOps auto-creates this repo)
            // - Otherwise, fall back to source repo name or project name
            var targetRepoName = !string.IsNullOrWhiteSpace(request.TargetRepoName) 
                ? request.TargetRepoName 
                : (!result.ProjectExisted 
                    ? projectName  // New project: use the auto-created repo with same name as project
                    : (repoInfo?.Name ?? request.NewProjectName ?? "imported-repo"));
            
            var targetBranchName = !string.IsNullOrWhiteSpace(request.TargetBranchName) 
                ? request.TargetBranchName 
                : "main";
            
            var existingRepos = await gitClient.GetRepositoriesAsync(projectId);
            var existingRepo = existingRepos.FirstOrDefault(r => 
                string.Equals(r.Name, targetRepoName, StringComparison.OrdinalIgnoreCase));
            
            if (existingRepo != null) {
                // Repo exists - we'll import to the specified branch
                result.RepoId = existingRepo.Id.ToString();
                result.RepoName = existingRepo.Name;
                result.RepoUrl = existingRepo.WebUrl ?? existingRepo.RemoteUrl;
                result.RepoExisted = true;
                
                if (!string.IsNullOrWhiteSpace(connectionId)) {
                    await SignalRUpdate(new DataObjects.SignalRUpdate {
                        UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                        ConnectionId = connectionId,
                        ItemId = Guid.NewGuid(),
                        Message = $"Using existing repository '{targetRepoName}', importing to branch '{targetBranchName}'..."
                    });
                }
            } else {
                // Create new repo
                var repoResult = await CreateDevOpsRepoAsync(pat, orgName, projectId, targetRepoName, connectionId);
                result.RepoId = repoResult.RepoId;
                result.RepoName = repoResult.RepoName;
                result.RepoUrl = repoResult.ResourceUrl;
                result.RepoExisted = false;
            }

            result.ImportedBranch = targetBranchName;

            // === Step 3: Import code as snapshot to specified branch ===
            switch (request.Method) {
                case DataObjects.ImportMethod.GitSnapshot:
                    return await ImportViaSnapshotAsync(pat, orgName, gitClient, projectId, result, repoInfo!, targetBranchName, request.CommitMessage, connectionId);
                
                case DataObjects.ImportMethod.ZipUpload:
                    return await ImportViaZipUploadAsync(pat, orgName, gitClient, projectId, result, request.UploadedFileId!.Value, targetBranchName, request.CommitMessage, request.SourceUrl, connectionId);
                
                default:
                    result.Success = false;
                    result.ErrorMessage = $"Unsupported import method: {request.Method}. Use GitSnapshot or ZipUpload.";
                    return result;
            }

        } catch (Exception ex) {
            result.Success = false;
            result.ErrorMessage = $"Error importing repository: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Import via downloading ZIP from source and pushing as a fresh snapshot to specified branch.
    /// </summary>
    private async Task<DataObjects.ImportPublicRepoResponse> ImportViaSnapshotAsync(
        string pat, 
        string orgName,
        GitHttpClient gitClient, 
        string projectId, 
        DataObjects.ImportPublicRepoResponse result, 
        DataObjects.PublicGitRepoInfo repoInfo,
        string targetBranchName,
        string? commitMessage,
        string? connectionId)
    {
        string? tempDir = null;
        
        try {
            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                    ConnectionId = connectionId,
                    ItemId = Guid.NewGuid(),
                    Message = "Downloading source code..."
                });
            }

            // Build download URL based on source
            var downloadUrl = GetZipDownloadUrl(repoInfo);
            if (string.IsNullOrWhiteSpace(downloadUrl)) {
                result.Success = false;
                result.ErrorMessage = "Cannot determine download URL for this repository source.";
                return result;
            }

            // Download ZIP to temp location
            tempDir = Path.Combine(Path.GetTempPath(), $"FreeCICD_Snapshot_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            var zipPath = Path.Combine(tempDir, "source.zip");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "FreeCICD/1.0");
            var response = await httpClient.GetAsync(downloadUrl);
            
            if (!response.IsSuccessStatusCode) {
                result.Success = false;
                result.ErrorMessage = $"Failed to download source: {response.StatusCode}";
                return result;
            }

            await using (var fileStream = new FileStream(zipPath, FileMode.Create)) {
                await response.Content.CopyToAsync(fileStream);
            }

            // Extract and push
            return await ExtractAndPushToRepoAsync(pat, orgName, gitClient, projectId, result, zipPath, targetBranchName,
                commitMessage ?? $"Initial import from {repoInfo.Source}: {repoInfo.Url}", connectionId);

        } finally {
            // Cleanup temp directory
            if (!string.IsNullOrWhiteSpace(tempDir) && Directory.Exists(tempDir)) {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    /// <summary>
    /// Import via user-uploaded ZIP file as a fresh snapshot to specified branch.
    /// </summary>
    private async Task<DataObjects.ImportPublicRepoResponse> ImportViaZipUploadAsync(
        string pat, 
        string orgName,
        GitHttpClient gitClient, 
        string projectId, 
        DataObjects.ImportPublicRepoResponse result, 
        Guid uploadedFileId,
        string targetBranchName,
        string? commitMessage,
        string? sourceUrl,
        string? connectionId)
    {
        try {
            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                    ConnectionId = connectionId,
                    ItemId = Guid.NewGuid(),
                    Message = "Processing uploaded ZIP file..."
                });
            }

            // Find uploaded file
            var uploadDir = Path.Combine(Path.GetTempPath(), "FreeCICD_Imports");
            var zipPath = Path.Combine(uploadDir, $"{uploadedFileId}.zip");

            if (!File.Exists(zipPath)) {
                result.Success = false;
                result.ErrorMessage = "Uploaded file not found or has expired. Please upload again.";
                return result;
            }

            var defaultCommitMessage = string.IsNullOrWhiteSpace(sourceUrl) 
                ? "Initial import from uploaded ZIP" 
                : $"Initial import from: {sourceUrl}";

            // Extract and push
            var importResult = await ExtractAndPushToRepoAsync(pat, orgName, gitClient, projectId, result, zipPath, targetBranchName,
                commitMessage ?? defaultCommitMessage, connectionId);

            // Clean up uploaded file after successful import
            try { File.Delete(zipPath); } catch { }

            return importResult;

        } catch (Exception ex) {
            result.Success = false;
            result.ErrorMessage = $"Error processing uploaded file: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Extracts a ZIP file and pushes contents to Azure DevOps repo as a commit on the specified branch.
    /// </summary>
    private async Task<DataObjects.ImportPublicRepoResponse> ExtractAndPushToRepoAsync(
        string pat,
        string orgName,
        GitHttpClient gitClient,
        string projectId,
        DataObjects.ImportPublicRepoResponse result,
        string zipPath,
        string targetBranchName,
        string commitMessage,
        string? connectionId)
    {
        string? extractDir = null;
        
        try {
            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                    ConnectionId = connectionId,
                    ItemId = Guid.NewGuid(),
                    Message = "Extracting files..."
                });
            }

            // Extract ZIP
            extractDir = Path.Combine(Path.GetTempPath(), $"FreeCICD_Extract_{Guid.NewGuid()}");
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            // Handle GitHub-style ZIP structure (reponame-branch/ wrapper directory)
            var extractedDirs = Directory.GetDirectories(extractDir);
            var sourceDir = extractDir;
            if (extractedDirs.Length == 1 && Directory.GetFiles(extractDir).Length == 0) {
                // Single directory wrapper - use it as source
                sourceDir = extractedDirs[0];
            }

            // Remove any .git directory
            var gitDir = Path.Combine(sourceDir, ".git");
            if (Directory.Exists(gitDir)) {
                Directory.Delete(gitDir, true);
            }

            // Build the list of files to push
            var filesToPush = new List<(string relativePath, byte[] content)>();
            foreach (var filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)) {
                var relativePath = Path.GetRelativePath(sourceDir, filePath).Replace('\\', '/');
                
                // Skip hidden files (except .github, .vscode)
                if (relativePath.StartsWith(".") && !relativePath.StartsWith(".github") && !relativePath.StartsWith(".vscode")) {
                    continue;
                }

                var content = await File.ReadAllBytesAsync(filePath);
                filesToPush.Add((relativePath, content));
            }

            if (filesToPush.Count == 0) {
                result.Success = false;
                result.ErrorMessage = "No files found in the ZIP archive.";
                return result;
            }

            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                    ConnectionId = connectionId,
                    ItemId = Guid.NewGuid(),
                    Message = $"Pushing {filesToPush.Count} files to branch '{targetBranchName}'..."
                });
            }

            // Create push with all files
            var changes = filesToPush.Select(f => new GitChange {
                ChangeType = VersionControlChangeType.Add,
                Item = new GitItem { Path = "/" + f.relativePath },
                NewContent = new ItemContent {
                    Content = Convert.ToBase64String(f.content),
                    ContentType = ItemContentType.Base64Encoded
                }
            }).ToList();

            // Determine the old object ID for the ref update
            // If repo is empty (new) or branch doesn't exist, use all zeros
            string oldObjectId = "0000000000000000000000000000000000000000";
            
            try {
                var refs = await gitClient.GetRefsAsync(projectId, result.RepoId, filter: $"heads/{targetBranchName}");
                var existingRef = refs.FirstOrDefault();
                if (existingRef != null) {
                    oldObjectId = existingRef.ObjectId;
                }
            } catch {
                // Branch doesn't exist, use all zeros (new branch)
            }

            var push = new GitPush {
                RefUpdates = new List<GitRefUpdate> {
                    new GitRefUpdate {
                        Name = $"refs/heads/{targetBranchName}",
                        OldObjectId = oldObjectId
                    }
                },
                Commits = new List<GitCommitRef> {
                    new GitCommitRef {
                        Comment = commitMessage,
                        Changes = changes
                    }
                }
            };

            var pushResult = await gitClient.CreatePushAsync(push, projectId, result.RepoId);

            result.Status = DataObjects.ImportStatus.Completed;
            result.Success = true;
            result.DetailedStatus = $"Pushed {filesToPush.Count} files to branch '{targetBranchName}'.";

            // === Phase 1: GitHub Sync - Populate PR URL for existing repos ===
            // If we imported to an existing repo, get the default branch and build the PR URL
            if (result.RepoExisted) {
                try {
                    var repo = await gitClient.GetRepositoryAsync(projectId, result.RepoId);
                    result.DefaultBranch = repo.DefaultBranch?.Replace("refs/heads/", "") ?? "main";
                    
                    // Only generate PR URL if we imported to a different branch than default
                    if (!string.Equals(targetBranchName, result.DefaultBranch, StringComparison.OrdinalIgnoreCase)) {
                        // Build the PR creation URL
                        // Format: https://dev.azure.com/{org}/{project}/_git/{repo}/pullrequestcreate?sourceRef={branch}&targetRef={default}
                        result.PullRequestCreateUrl = $"https://dev.azure.com/{orgName}/" +
                            $"{Uri.EscapeDataString(result.ProjectName!)}/_git/" +
                            $"{Uri.EscapeDataString(result.RepoName!)}/pullrequestcreate" +
                            $"?sourceRef={Uri.EscapeDataString(targetBranchName)}" +
                            $"&targetRef={Uri.EscapeDataString(result.DefaultBranch)}";
                    }
                } catch {
                    // Fallback - still successful import, just no PR URL
                    result.DefaultBranch = "main";
                }
            }

            return result;

        } catch (Exception ex) {
            result.Success = false;
            result.ErrorMessage = $"Error pushing to repository: {ex.Message}";
            result.Status = DataObjects.ImportStatus.Failed;
            return result;
        } finally {
            // Cleanup
            if (!string.IsNullOrWhiteSpace(extractDir) && Directory.Exists(extractDir)) {
                try { Directory.Delete(extractDir, true); } catch { }
            }
        }
    }

    /// <summary>
    /// Gets the ZIP download URL for a repository based on its source.
    /// </summary>
    private string? GetZipDownloadUrl(DataObjects.PublicGitRepoInfo repoInfo)
    {
        return repoInfo.Source?.ToLowerInvariant() switch {
            "github" => $"https://github.com/{repoInfo.Owner}/{repoInfo.Name}/archive/refs/heads/{repoInfo.DefaultBranch}.zip",
            "gitlab" => $"https://gitlab.com/{repoInfo.Owner}/{repoInfo.Name}/-/archive/{repoInfo.DefaultBranch}/{repoInfo.Name}-{repoInfo.DefaultBranch}.zip",
            "bitbucket" => $"https://bitbucket.org/{repoInfo.Owner}/{repoInfo.Name}/get/{repoInfo.DefaultBranch}.zip",
            _ => null
        };
    }

    /// <summary>
    /// Gets the status of a repository import operation.
    /// </summary>
    public async Task<DataObjects.ImportPublicRepoResponse> GetImportStatusAsync(string pat, string orgName, string projectId, string repoId, int importRequestId, string? connectionId = null)
    {
        var result = new DataObjects.ImportPublicRepoResponse {
            ProjectId = projectId,
            RepoId = repoId,
            ImportRequestId = importRequestId
        };

        try {
            using var connection = CreateConnection(pat, orgName);
            var gitClient = connection.GetClient<GitHttpClient>();

            var importRequest = await gitClient.GetImportRequestAsync(
                projectId,
                new Guid(repoId),
                importRequestId
            );

            result.Status = MapImportStatus(importRequest.Status);
            result.Success = result.Status == DataObjects.ImportStatus.Completed;

            if (importRequest.Status == GitAsyncOperationStatus.Failed) {
                result.Success = false;
                result.ErrorMessage = importRequest.DetailedStatus?.ErrorMessage ?? "Import failed.";
                result.DetailedStatus = importRequest.DetailedStatus?.AllSteps?.LastOrDefault()?.ToString();
            }

            // Get the repo URL
            if (result.Status == DataObjects.ImportStatus.Completed) {
                try {
                    var repo = await gitClient.GetRepositoryAsync(projectId, repoId);
                    if (repo.Links?.Links != null && repo.Links.Links.ContainsKey("web")) {
                        dynamic webLink = repo.Links.Links["web"];
                        result.RepoUrl = webLink.Href;
                    } else {
                        result.RepoUrl = repo.WebUrl ?? repo.RemoteUrl ?? string.Empty;
                    }
                } catch {
                    // Ignore errors getting repo URL
                }
            }

            return result;

        } catch (Exception ex) {
            result.Success = false;
            result.ErrorMessage = $"Error checking import status: {ex.Message}";
            return result;
        }
    }

    private static DataObjects.ImportStatus MapImportStatus(GitAsyncOperationStatus status)
    {
        return status switch {
            GitAsyncOperationStatus.Queued => DataObjects.ImportStatus.Queued,
            GitAsyncOperationStatus.InProgress => DataObjects.ImportStatus.InProgress,
            GitAsyncOperationStatus.Completed => DataObjects.ImportStatus.Completed,
            GitAsyncOperationStatus.Failed => DataObjects.ImportStatus.Failed,
            GitAsyncOperationStatus.Abandoned => DataObjects.ImportStatus.Failed,
            _ => DataObjects.ImportStatus.NotStarted
        };
    }
}

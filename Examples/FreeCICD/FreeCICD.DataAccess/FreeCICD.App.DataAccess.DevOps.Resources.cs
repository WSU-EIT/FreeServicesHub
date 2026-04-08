using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace FreeCICD;

// DevOps Resource Operations: Projects, Repos, Branches, Variable Groups

public partial class DataAccess
{
    public async Task<DataObjects.DevopsVariableGroup> CreateVariableGroup(string projectId, string pat, string orgName, DataObjects.DevopsVariableGroup newGroup, string? connectionId = null)
    {
        using (var connection = CreateConnection(pat, orgName)) {
            var taskAgentClient = connection.GetClient<TaskAgentHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();

            try {
                TeamProjectReference project = await projectClient.GetProject(projectId);

                var parameters = new VariableGroupParameters {
                    Name = newGroup.Name,
                    Description = newGroup.Description,
                    Type = "Vsts",
                    Variables = newGroup.Variables.ToDictionary(
                        kv => kv.Name,
                        kv => new VariableValue {
                            Value = kv.Value,
                            IsSecret = kv.IsSecret,
                            IsReadOnly = kv.IsReadOnly
                        },
                        StringComparer.OrdinalIgnoreCase),
                    VariableGroupProjectReferences = [new VariableGroupProjectReference {
                        Name = newGroup.Name,
                        Description = project.Description,
                        ProjectReference = new ProjectReference {
                            Id = project.Id,
                            Name = project.Name
                        }
                    }]
                };

                var createdGroup = await taskAgentClient.AddVariableGroupAsync(parameters, new Guid(projectId), cancellationToken: CancellationToken.None);

                var mappedGroup = new DataObjects.DevopsVariableGroup {
                    Id = createdGroup.Id,
                    Name = createdGroup.Name,
                    Description = createdGroup.Description,
                    Variables = createdGroup.Variables.ToDictionary(
                        kv => kv.Key,
                        kv => new DataObjects.DevopsVariable {
                            Name = kv.Key,
                            Value = kv.Value.Value,
                            IsSecret = kv.Value.IsSecret,
                            IsReadOnly = kv.Value.IsReadOnly
                        }).Values.ToList(),
                    ResourceUrl = string.Empty
                };

                return mappedGroup;
            } catch (Exception ex) {
                throw new Exception("Error creating variable group: " + ex.Message);
            }
        }
    }

    public async Task<DataObjects.DevopsGitRepoBranchInfo> GetDevOpsBranchAsync(string pat, string orgName, string projectId, string repoId, string branchName, string? connectionId = null)
    {
        var output = new DataObjects.DevopsGitRepoBranchInfo();

        if (!string.IsNullOrWhiteSpace(connectionId)) {
            await SignalRUpdate(new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                ConnectionId = connectionId,
                ItemId = Guid.NewGuid(),
                Message = "Start of lookup of branch"
            });
        }

        using (var connection = CreateConnection(pat, orgName)) {
            var gitClient = connection.GetClient<GitHttpClient>();
            try {
                var repo = await gitClient.GetRepositoryAsync(projectId, repoId);
                dynamic repoResource = repo.Links.Links["web"];

                var repoInfo = new DataObjects.DevopsGitRepoInfo {
                    RepoName = repo.Name,
                    RepoId = repoId.ToString(),
                    ResourceUrl = repoResource.Href
                };

                var branch = await gitClient.GetBranchAsync(repoId, branchName);
                var branchInfo = new DataObjects.DevopsGitRepoBranchInfo {
                    BranchName = branch.Name,
                    LastCommitDate = branch?.Commit?.Committer?.Date
                };

                var branchDisplayName = string.Empty + branch?.Name?.Replace("refs/heads/", "");
                branchInfo.ResourceUrl = $"{repoInfo.ResourceUrl}?version=GB{Uri.EscapeDataString(branchDisplayName)}";

                if (!string.IsNullOrWhiteSpace(connectionId)) {
                    await SignalRUpdate(new DataObjects.SignalRUpdate {
                        UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                        ConnectionId = connectionId,
                        ItemId = Guid.NewGuid(),
                        Message = $"Found branch {branch?.Name} in repo {repo?.Name}"
                    });
                }

                output = branchInfo;
            } catch (Exception) {
                // Error fetching branch
            }
        }

        return output;
    }

    public async Task<List<DataObjects.DevopsGitRepoBranchInfo>> GetDevOpsBranchesAsync(string pat, string orgName, string projectId, string repoId, string? connectionId = null)
    {
        var output = new List<DataObjects.DevopsGitRepoBranchInfo>();

        if (!string.IsNullOrWhiteSpace(connectionId)) {
            await SignalRUpdate(new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                ConnectionId = connectionId,
                ItemId = Guid.NewGuid(),
                Message = "Start of lookup"
            });
        }

        using (var connection = CreateConnection(pat, orgName)) {
            var gitClient = connection.GetClient<GitHttpClient>();
            try {
                var repo = await gitClient.GetRepositoryAsync(projectId, repoId);
                dynamic repoResource = repo.Links.Links["web"];

                var repoInfo = new DataObjects.DevopsGitRepoInfo {
                    RepoName = repo.Name,
                    RepoId = repoId.ToString(),
                    ResourceUrl = repoResource.Href
                };

                var branches = await gitClient.GetBranchesAsync(projectId, repoId);
                if (branches != null && branches.Any()) {
                    foreach (var branch in branches) {
                        try {
                            var branchInfo = new DataObjects.DevopsGitRepoBranchInfo {
                                BranchName = branch.Name,
                                LastCommitDate = branch?.Commit?.Committer?.Date
                            };

                            var branchDisplayName = string.Empty + branch?.Name?.Replace("refs/heads/", "");
                            branchInfo.ResourceUrl = $"{repoInfo.ResourceUrl}?version=GB{Uri.EscapeDataString(branchDisplayName)}";

                            if (!string.IsNullOrWhiteSpace(connectionId)) {
                                await SignalRUpdate(new DataObjects.SignalRUpdate {
                                    UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                                    ConnectionId = connectionId,
                                    ItemId = Guid.NewGuid(),
                                    Message = $"Found branch {branch?.Name} in repo {repo?.Name}"
                                });
                            }

                            output.Add(branchInfo);
                        } catch (Exception) {
                            // Error processing branch
                        }
                    }
                }
            } catch (Exception) {
                // Error fetching branches
            }
        }

        return output;
    }

    public async Task<List<DataObjects.DevopsFileItem>> GetDevOpsFilesAsync(string pat, string orgName, string projectId, string repoId, string branchName, string? connectionId = null)
    {
        var output = new List<DataObjects.DevopsFileItem>();

        if (!string.IsNullOrWhiteSpace(connectionId)) {
            await SignalRUpdate(new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                ConnectionId = connectionId,
                ItemId = Guid.NewGuid(),
                Message = "Start of lookup"
            });
        }

        using (var connection = CreateConnection(pat, orgName)) {
            var gitClient = connection.GetClient<GitHttpClient>();

            var repo = await gitClient.GetRepositoryAsync(projectId, repoId);
            dynamic repoResource = repo.Links.Links["web"];

            var repoInfo = new DataObjects.DevopsGitRepoInfo {
                RepoName = repo.Name,
                RepoId = repoId.ToString(),
                ResourceUrl = repoResource.Href
            };

            var branch = await gitClient.GetBranchAsync(repoId, branchName);

            var branchInfo = new DataObjects.DevopsGitRepoBranchInfo {
                BranchName = branch.Name,
                LastCommitDate = branch?.Commit?.Committer?.Date
            };

            try {
                var versionDescriptor = new GitVersionDescriptor {
                    Version = branchName,
                    VersionType = GitVersionType.Branch
                };

                var items = await gitClient.GetItemsAsync(
                    project: projectId.ToString(),
                    repositoryId: repoId.ToString(),
                    scopePath: null,
                    recursionLevel: VersionControlRecursionType.Full,
                    includeLinks: true,
                    versionDescriptor: versionDescriptor);

                if (items != null && items.Any()) {
                    foreach (var item in items) {
                        if (!item.IsFolder) {
                            var resourceUrl = string.Empty;
                            string marker = "/items//";
                            var url = item.Url;
                            int markerIndex = url.IndexOf(marker);
                            if (markerIndex >= 0) {
                                string rightPart = url.Substring(markerIndex + marker.Length);
                                var path = rightPart.Split("?")[0];
                                resourceUrl = $"{branchInfo.ResourceUrl}&path=/" + path;
                            }

                            var fileItem = new DataObjects.DevopsFileItem {
                                Path = item.Path,
                                FileType = Path.GetExtension(item.Path),
                                ResourceUrl = resourceUrl
                            };

                            if (!string.IsNullOrWhiteSpace(connectionId)) {
                                if (fileItem.FileType == ".csproj" || fileItem.FileType == ".yml") {
                                    await SignalRUpdate(new DataObjects.SignalRUpdate {
                                        UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                                        ConnectionId = connectionId,
                                        ItemId = Guid.NewGuid(),
                                        Message = $"Found file {fileItem.Path} in branch {branch?.Name} in repo {repo?.Name}"
                                    });
                                }
                            }

                            output.Add(fileItem);
                        }
                    }
                }
            } catch (Exception) {
                // Error fetching file structure
            }
        }

        return output;
    }

    public async Task<DataObjects.DevopsProjectInfo> GetDevOpsProjectAsync(string pat, string orgName, string projectId, string? connectionId = null)
    {
        var output = new DataObjects.DevopsProjectInfo();

        if (!string.IsNullOrWhiteSpace(connectionId)) {
            await SignalRUpdate(new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                ConnectionId = connectionId,
                ItemId = Guid.NewGuid(),
                Message = "Start of lookup project"
            });
        }

        using (var connection = CreateConnection(pat, orgName)) {
            try {
                var projectClient = connection.GetClient<ProjectHttpClient>();
                var project = await projectClient.GetProject(projectId);
                var projInfo = new DataObjects.DevopsProjectInfo {
                    ProjectName = project.Name,
                    ProjectId = project.Id.ToString(),
                    CreationDate = project.LastUpdateTime,
                    GitRepos = new List<DataObjects.DevopsGitRepoInfo>(),
                };

                dynamic projectResource = project.Links.Links["web"];
                projInfo.ResourceUrl = string.Empty + projectResource.Href;

                if (!string.IsNullOrWhiteSpace(connectionId)) {
                    await SignalRUpdate(new DataObjects.SignalRUpdate {
                        UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                        ConnectionId = connectionId,
                        ItemId = Guid.NewGuid(),
                        Message = "found project " + output.ProjectName
                    });
                }
                output = projInfo;
            } catch (Exception) {
                // Error fetching project
            }
        }

        return output;
    }

    public async Task<List<DataObjects.DevopsProjectInfo>> GetDevOpsProjectsAsync(string pat, string orgName, string? connectionId = null)
    {
        var output = new List<DataObjects.DevopsProjectInfo>();

        if (!string.IsNullOrWhiteSpace(connectionId)) {
            await SignalRUpdate(new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                ConnectionId = connectionId,
                ItemId = Guid.NewGuid(),
                Message = "Start of lookup"
            });
        }

        using (var connection = CreateConnection(pat, orgName)) {
            try {
                var projectClient = connection.GetClient<ProjectHttpClient>();
                List<TeamProjectReference> projects = new List<TeamProjectReference>();
                try {
                    projects = (await projectClient.GetProjects()).ToList();
                    projects = projects.Where(o => !GlobalSettings.App.AzureDevOpsProjectNameStartsWithIgnoreValues.Any(v => (string.Empty + o.Name).ToLower().StartsWith((string.Empty + v).ToLower()))).ToList();
                } catch (Exception) {
                    // Error fetching projects
                }

                var projectTasks = projects.Select(async project => {
                    var projInfo = new DataObjects.DevopsProjectInfo {
                        ProjectName = project.Name,
                        ProjectId = project.Id.ToString(),
                        CreationDate = project.LastUpdateTime,
                        GitRepos = new List<DataObjects.DevopsGitRepoInfo>(),
                    };

                    if (!string.IsNullOrWhiteSpace(connectionId)) {
                        await SignalRUpdate(new DataObjects.SignalRUpdate {
                            UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                            ConnectionId = connectionId,
                            ItemId = Guid.NewGuid(),
                            Message = "found project " + projInfo.ProjectName
                        });
                    }

                    var p = await projectClient.GetProject(project.Id.ToString());
                    dynamic projectResource = p.Links.Links["web"];
                    projInfo.ResourceUrl = string.Empty + projectResource.Href;

                    return projInfo;
                });

                var projectInfos = await Task.WhenAll(projectTasks);
                output.AddRange(projectInfos);
            } catch (Exception) {
                // Error during DevOps connection processing
            }
        }

        return output;
    }

    public async Task<DataObjects.DevopsGitRepoInfo> GetDevOpsRepoAsync(string pat, string orgName, string projectId, string repoId, string? connectionId = null)
    {
        var output = new DataObjects.DevopsGitRepoInfo();

        if (!string.IsNullOrWhiteSpace(connectionId)) {
            await SignalRUpdate(new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                ConnectionId = connectionId,
                ItemId = Guid.NewGuid(),
                Message = "Start of lookup"
            });
        }

        using (var connection = CreateConnection(pat, orgName)) {
            try {
                var gitClient = connection.GetClient<GitHttpClient>();
                var gitRepos = await gitClient.GetRepositoriesAsync(projectId);
                if (gitRepos.Count > 0) {
                    var repo = await gitClient.GetRepositoryAsync(projectId, repoId);
                    dynamic repoResource = repo.Links.Links["web"];

                    var repoInfo = new DataObjects.DevopsGitRepoInfo {
                        RepoName = repo.Name,
                        RepoId = repo.Id.ToString(),
                    };

                    repoInfo.ResourceUrl = repoResource.Href;

                    if (!string.IsNullOrWhiteSpace(connectionId)) {
                        await SignalRUpdate(new DataObjects.SignalRUpdate {
                            UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                            ConnectionId = connectionId,
                            ItemId = Guid.NewGuid(),
                            Message = $"Found {repo.Name}"
                        });
                    }

                    output = repoInfo;
                }
            } catch (Exception) {
                // Error fetching Git repositories
            }
        }

        return output;
    }

    public async Task<List<DataObjects.DevopsGitRepoInfo>> GetDevOpsReposAsync(string pat, string orgName, string projectId, string? connectionId = null)
    {
        var output = new List<DataObjects.DevopsGitRepoInfo>();

        if (!string.IsNullOrWhiteSpace(connectionId)) {
            await SignalRUpdate(new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                ConnectionId = connectionId,
                ItemId = Guid.NewGuid(),
                Message = "Start of lookup"
            });
        }

        using (var connection = CreateConnection(pat, orgName)) {
            try {
                var gitClient = connection.GetClient<GitHttpClient>();
                var gitRepos = await gitClient.GetRepositoriesAsync(projectId);

                if (gitRepos.Count > 0) {
                    var repoTasks = gitRepos.Select(async repo => {
                        var repoInfo = new DataObjects.DevopsGitRepoInfo {
                            RepoName = repo.Name,
                            RepoId = repo.Id.ToString(),
                        };

                        var r = await gitClient.GetRepositoryAsync(projectId, repo.Id);
                        dynamic repoResource = r.Links.Links["web"];
                        repoInfo.ResourceUrl = repoResource.Href;

                        if (!string.IsNullOrWhiteSpace(connectionId)) {
                            await SignalRUpdate(new DataObjects.SignalRUpdate {
                                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                                ConnectionId = connectionId,
                                ItemId = Guid.NewGuid(),
                                Message = $"Found {repo.Name}"
                            });
                        }

                        return repoInfo;
                    });

                    var repos = await Task.WhenAll(repoTasks);
                    output.AddRange(repos);
                }
            } catch (Exception) {
                // Error fetching Git repositories
            }
        }

        return output;
    }

    public async Task<DataObjects.DevopsVariableGroup> UpdateVariableGroup(string projectId, string pat, string orgName, DataObjects.DevopsVariableGroup updatedGroup, string? connectionId = null)
    {
        using (var connection = CreateConnection(pat, orgName)) {
            var taskAgentClient = connection.GetClient<TaskAgentHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();

            TeamProjectReference project = await projectClient.GetProject(projectId);

            var devopsVariableGroups = await taskAgentClient.GetVariableGroupsAsync(new Guid(projectId));
            var group = devopsVariableGroups.FirstOrDefault(g => g.Id == updatedGroup.Id);

            var parameters = new VariableGroupParameters {
                Name = updatedGroup.Name,
                Description = updatedGroup.Description,
                Type = "Vsts",
                Variables = updatedGroup.Variables.ToDictionary(
                    kv => kv.Name,
                    kv => new VariableValue {
                        Value = kv.Value,
                        IsSecret = kv.IsSecret,
                        IsReadOnly = kv.IsReadOnly
                    },
                    StringComparer.OrdinalIgnoreCase),
                VariableGroupProjectReferences = [new VariableGroupProjectReference {
                    Name = project.Name,
                    Description = project.Description,
                    ProjectReference = new ProjectReference {
                        Id = project.Id,
                        Name = project.Name
                    }
                }]
            };

            try {
                var updatedVariableGroup = await taskAgentClient.UpdateVariableGroupAsync(group!.Id, parameters, cancellationToken: CancellationToken.None);
                var mappedGroup = new DataObjects.DevopsVariableGroup {
                    Id = updatedVariableGroup.Id,
                    Name = updatedVariableGroup.Name,
                    Description = updatedVariableGroup.Description,
                    Variables = updatedVariableGroup.Variables
                        .ToDictionary(kvp => kvp.Key, kvp => new DataObjects.DevopsVariable {
                            Name = kvp.Key,
                            Value = kvp.Value.Value,
                            IsSecret = kvp.Value.IsSecret,
                            IsReadOnly = kvp.Value.IsReadOnly
                        }).Values.ToList(),
                    ResourceUrl = string.Empty
                };
                return mappedGroup;
            } catch (Exception ex) {
                throw new Exception("Error updating variable group: " + ex.Message);
            }
        }
    }

    public async Task<List<DataObjects.DevopsVariableGroup>> GetProjectVariableGroupsAsync(string pat, string orgName, string projectId, string? connectionId = null)
    {
        var connection = CreateConnection(pat, orgName);
        var variableGroups = new List<DataObjects.DevopsVariableGroup>();

        try {
            var taskAgentClient = connection.GetClient<TaskAgentHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();

            var project = await projectClient.GetProject(projectId);
            dynamic projectResource = project.Links.Links["web"];
            var projectUrl = Uri.EscapeUriString(string.Empty + projectResource.Href);

            var devopsVariableGroups = await taskAgentClient.GetVariableGroupsAsync(project.Id);

            variableGroups = devopsVariableGroups.Select(g => {
                var group = taskAgentClient.GetVariableGroupAsync(project.Id, g.Id).Result;

                var vargroup = new DataObjects.DevopsVariableGroup {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    ResourceUrl = $"{projectUrl}/_library?itemType=VariableGroups&view=VariableGroupView&variableGroupId={g.Id}",
                    Variables = g.Variables.Select(v => new DataObjects.DevopsVariable {
                        Name = v.Key,
                        Value = v.Value.Value,
                        IsSecret = v.Value.IsSecret,
                        IsReadOnly = v.Value.IsReadOnly
                    }).ToList()
                };

                return vargroup;
            }).ToList();
        } catch (Exception) {
            // Error getting variable groups
        }

        return variableGroups;
    }
}

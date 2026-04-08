using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace FreeCICD;

// Dashboard Operations: Pipeline dashboard queries and YAML parsing

public partial class DataAccess
{
    /// <summary>
    /// Gets the pipeline dashboard with progressive loading via SignalR.
    /// Sends skeleton data immediately, then enriches with details in batches.
    /// </summary>
    public async Task<DataObjects.PipelineDashboardResponse> GetPipelineDashboardAsync(string pat, string orgName, string projectId, string? connectionId = null)
    {
        var response = new DataObjects.PipelineDashboardResponse();

        try {
            // === IMMEDIATE: Signal that loading has started (before any API calls) ===
            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.DashboardPipelinesSkeleton,
                    ConnectionId = connectionId,
                    Message = "Connecting to Azure DevOps...",
                    Object = new List<DataObjects.PipelineListItem>() // Empty list to trigger UI
                });
            }

            using var connection = CreateConnection(pat, orgName);
            var buildClient = connection.GetClient<BuildHttpClient>();
            var gitClient = connection.GetClient<GitHttpClient>();
            var taskAgentClient = connection.GetClient<TaskAgentHttpClient>();
            var projectClient = connection.GetClient<ProjectHttpClient>();

            // Get project info for variable group URLs
            var project = await projectClient.GetProject(projectId);
            dynamic projectResource = project.Links.Links["web"];
            var projectUrl = Uri.EscapeUriString(string.Empty + projectResource.Href);
            var baseUrl = $"https://dev.azure.com/{orgName}/{project.Name}";

            // === PHASE 1: Get pipeline definitions quickly (skeleton) ===
            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                    ConnectionId = connectionId,
                    Message = "Fetching pipeline list..."
                });
            }

            var definitions = await buildClient.GetDefinitionsAsync(project: projectId);
            var pipelineItems = new List<DataObjects.PipelineListItem>();

            // Create skeleton items with just basic info (fast)
            foreach (var defRef in definitions) {
                var item = new DataObjects.PipelineListItem {
                    Id = defRef.Id,
                    Name = defRef?.Name ?? string.Empty,
                    Path = defRef?.Path ?? string.Empty,
                    PipelineRunsUrl = $"{baseUrl}/_build?definitionId={defRef?.Id}",
                    EditWizardUrl = $"Wizard?import={defRef?.Id}",
                    VariableGroups = []
                };
                pipelineItems.Add(item);
            }

            // Send skeleton with names immediately via SignalR
            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.DashboardPipelinesSkeleton,
                    ConnectionId = connectionId,
                    Message = $"Found {pipelineItems.Count} pipelines",
                    Object = pipelineItems
                });
            }

            // === PHASE 2: Fetch variable groups (needed for enrichment) ===
            var variableGroupsDict = new Dictionary<string, DataObjects.DevopsVariableGroup>(StringComparer.OrdinalIgnoreCase);
            try {
                var devopsVariableGroups = await taskAgentClient.GetVariableGroupsAsync(project.Id);
                foreach (var g in devopsVariableGroups) {
                    var vargroup = new DataObjects.DevopsVariableGroup {
                        Id = g.Id,
                        Name = g.Name,
                        Description = g.Description,
                        ResourceUrl = $"{projectUrl}/_library?itemType=VariableGroups&view=VariableGroupView&variableGroupId={g.Id}",
                        Variables = g.Variables.Select(v => new DataObjects.DevopsVariable {
                            Name = v.Key,
                            Value = v.Value.IsSecret ? "******" : v.Value.Value,
                            IsSecret = v.Value.IsSecret,
                            IsReadOnly = v.Value.IsReadOnly
                        }).ToList()
                    };
                    variableGroupsDict[g.Name] = vargroup;
                    response.AvailableVariableGroups.Add(vargroup);
                }
            } catch {
                // Error getting variable groups, continue without them
            }

            // === PHASE 3: Enrich each pipeline with details (send in batches) ===
            const int batchSize = 3;
            var enrichedBatch = new List<DataObjects.PipelineListItem>();
            int processedCount = 0;

            for (int i = 0; i < pipelineItems.Count; i++) {
                var item = pipelineItems[i];

                // Send per-pipeline status update so the UI shows what's being loaded
                if (!string.IsNullOrWhiteSpace(connectionId)) {
                    await SignalRUpdate(new DataObjects.SignalRUpdate {
                        UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                        ConnectionId = connectionId,
                        Message = $"Loading {item.Name}..."
                    });
                }

                try {
                    await EnrichPipelineItemAsync(item, buildClient, gitClient, projectId, project, projectUrl, baseUrl, orgName, variableGroupsDict);
                } catch {
                    // Error enriching pipeline, leave it with skeleton data
                }

                enrichedBatch.Add(item);
                processedCount++;

                // Send batch update via SignalR
                if (enrichedBatch.Count >= batchSize || i == pipelineItems.Count - 1) {
                    // Build a names summary for the batch
                    var batchNames = string.Join(", ", enrichedBatch.Select(b => b.Name));

                    if (!string.IsNullOrWhiteSpace(connectionId)) {
                        await SignalRUpdate(new DataObjects.SignalRUpdate {
                            UpdateType = DataObjects.SignalRUpdateType.DashboardPipelineBatch,
                            ConnectionId = connectionId,
                            Message = $"Loaded {processedCount} of {pipelineItems.Count} pipelines|{batchNames}",
                            Object = enrichedBatch.ToList() // Send copy of batch
                        });
                    }
                    enrichedBatch.Clear();
                }
            }

            // === PHASE 4: Signal completion ===
            if (!string.IsNullOrWhiteSpace(connectionId)) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    UpdateType = DataObjects.SignalRUpdateType.DashboardLoadComplete,
                    ConnectionId = connectionId,
                    Message = $"Loaded {pipelineItems.Count} pipelines"
                });
            }

            response.Pipelines = pipelineItems;
            response.TotalCount = pipelineItems.Count;
            response.Success = true;
        } catch (Exception ex) {
            response.Success = false;
            response.ErrorMessage = $"Error loading pipeline dashboard: {ex.Message}";
        }

        return response;
    }

    /// <summary>
    /// Enriches a pipeline item with detailed information (builds, YAML, variable groups).
    /// </summary>
    private async Task EnrichPipelineItemAsync(
        DataObjects.PipelineListItem item,
        BuildHttpClient buildClient,
        GitHttpClient gitClient,
        string projectId,
        TeamProject project,
        string projectUrl,
        string baseUrl,
        string orgName,
        Dictionary<string, DataObjects.DevopsVariableGroup> variableGroupsDict)
    {
        var fullDef = await buildClient.GetDefinitionAsync(projectId, item.Id);
        dynamic pipelineReferenceLink = fullDef.Links.Links["web"];
        var pipelineUrl = Uri.EscapeUriString(string.Empty + pipelineReferenceLink.Href);

        string yamlFilename = string.Empty;
        if (fullDef.Process is YamlProcess yamlProcess) {
            yamlFilename = yamlProcess.YamlFilename;
        }

        item.RepositoryName = fullDef?.Repository?.Name ?? string.Empty;
        item.DefaultBranch = fullDef?.Repository?.DefaultBranch ?? string.Empty;
        item.ResourceUrl = pipelineUrl;
        item.YamlFileName = yamlFilename;

        // Get the latest build for this pipeline (ordered by queue time to catch in-progress builds)
        try {
            var builds = await buildClient.GetBuildsAsync(projectId, definitions: [item.Id], top: 1, queryOrder: BuildQueryOrder.QueueTimeDescending);
            if (builds.Count > 0) {
                var latestBuild = builds[0];
                item.LastRunStatus = latestBuild.Status?.ToString() ?? string.Empty;
                item.LastRunResult = latestBuild.Result?.ToString() ?? string.Empty;
                item.LastRunTime = latestBuild.FinishTime ?? latestBuild.StartTime ?? latestBuild.QueueTime;
                item.TriggerBranch = latestBuild.SourceBranch;

                // Build ID and number
                item.LastRunBuildId = latestBuild.Id;
                item.LastRunBuildNumber = latestBuild.BuildNumber;
                
                // Duration calculation
                if (latestBuild.StartTime.HasValue && latestBuild.FinishTime.HasValue) {
                    item.Duration = latestBuild.FinishTime.Value - latestBuild.StartTime.Value;
                }
                
                // Commit hash (short and full versions)
                if (!string.IsNullOrWhiteSpace(latestBuild.SourceVersion)) {
                    item.LastCommitIdFull = latestBuild.SourceVersion;
                    item.LastCommitId = latestBuild.SourceVersion.Length > 7 
                        ? latestBuild.SourceVersion[..7] 
                        : latestBuild.SourceVersion;
                }

                // Map trigger information
                MapBuildTriggerInfo(latestBuild, item);

                // Get stage bubbles from build timeline
                if (item.LastRunBuildId.HasValue) {
                    item.Stages = await GetBuildStageBubblesAsync(buildClient, projectId, item.LastRunBuildId.Value);
                }
            }
        } catch {
            // Could not get latest build, leave status fields empty
        }

        // Build URLs
        if (!string.IsNullOrWhiteSpace(item.RepositoryName)) {
            item.RepositoryUrl = $"{baseUrl}/_git/{Uri.EscapeDataString(item.RepositoryName)}";
        }
        
        if (!string.IsNullOrWhiteSpace(item.LastCommitIdFull) && !string.IsNullOrWhiteSpace(item.RepositoryName)) {
            item.CommitUrl = $"{baseUrl}/_git/{Uri.EscapeDataString(item.RepositoryName)}/commit/{item.LastCommitIdFull}";
        }
        
        if (item.LastRunBuildId.HasValue) {
            item.LastRunResultsUrl = $"{baseUrl}/_build/results?buildId={item.LastRunBuildId}&view=results";
            item.LastRunLogsUrl = $"{baseUrl}/_build/results?buildId={item.LastRunBuildId}&view=logs";
        }
        
        var configBranch = !string.IsNullOrWhiteSpace(item.TriggerBranch) 
            ? item.TriggerBranch.Replace("refs/heads/", "") 
            : item.DefaultBranch?.Replace("refs/heads/", "") ?? "main";
        item.PipelineConfigUrl = $"{baseUrl}/_apps/hub/ms.vss-build-web.ci-designer-hub?pipelineId={item.Id}&branch={Uri.EscapeDataString(configBranch)}";

        // Parse YAML to extract variable groups and code repo info
        if (!string.IsNullOrWhiteSpace(yamlFilename) && fullDef?.Repository != null) {
            try {
                var repoId = fullDef.Repository.Id;
                var branch = fullDef.Repository.DefaultBranch?.Replace("refs/heads/", "") ?? "main";
                
                var versionDescriptor = new GitVersionDescriptor {
                    Version = branch,
                    VersionType = GitVersionType.Branch
                };

                var yamlItem = await gitClient.GetItemAsync(
                    project: projectId,
                    repositoryId: repoId,
                    path: yamlFilename,
                    scopePath: null,
                    recursionLevel: VersionControlRecursionType.None,
                    includeContent: true,
                    versionDescriptor: versionDescriptor);

                if (!string.IsNullOrWhiteSpace(yamlItem?.Content)) {
                    var parsedSettings = ParsePipelineYaml(yamlItem.Content, item.Id, item.Name, item.Path);
                    
                    // Populate Code Repo Info from YAML BuildRepo
                    if (!string.IsNullOrWhiteSpace(parsedSettings.CodeRepoName)) {
                        item.CodeProjectName = parsedSettings.CodeProjectName;
                        item.CodeRepoName = parsedSettings.CodeRepoName;
                        item.CodeBranch = parsedSettings.CodeBranch;
                        var codeProject = !string.IsNullOrWhiteSpace(parsedSettings.CodeProjectName) ? parsedSettings.CodeProjectName : project.Name;
                        item.CodeRepoUrl = $"https://dev.azure.com/{orgName}/{Uri.EscapeDataString(codeProject)}/_git/{Uri.EscapeDataString(parsedSettings.CodeRepoName)}";
                        
                        if (!string.IsNullOrWhiteSpace(parsedSettings.CodeBranch)) {
                            item.CodeBranchUrl = $"https://dev.azure.com/{orgName}/{Uri.EscapeDataString(codeProject)}/_git/{Uri.EscapeDataString(parsedSettings.CodeRepoName)}?version=GB{Uri.EscapeDataString(parsedSettings.CodeBranch)}";
                        }
                        
                        if (!string.IsNullOrWhiteSpace(item.LastCommitIdFull)) {
                            item.CommitUrl = $"https://dev.azure.com/{orgName}/{Uri.EscapeDataString(codeProject)}/_git/{Uri.EscapeDataString(parsedSettings.CodeRepoName)}/commit/{item.LastCommitIdFull}";
                        }
                    }
                    
                    // Extract variable groups from parsed environments
                    foreach (var env in parsedSettings.Environments) {
                        if (!string.IsNullOrWhiteSpace(env.VariableGroupName)) {
                            var vgRef = new DataObjects.PipelineVariableGroupRef {
                                Name = env.VariableGroupName,
                                Environment = env.EnvironmentName,
                                Id = null,
                                VariableCount = 0,
                                ResourceUrl = null
                            };

                            var vgName = env.VariableGroupName.Trim();
                            DataObjects.DevopsVariableGroup? matchedGroup = null;
                            
                            if (variableGroupsDict.TryGetValue(vgName, out matchedGroup)) {
                                // Found exact match
                            } else {
                                matchedGroup = variableGroupsDict.Values
                                    .FirstOrDefault(vg => vg.Name != null && 
                                        (vg.Name.Equals(vgName, StringComparison.OrdinalIgnoreCase) ||
                                         vg.Name.Contains(vgName, StringComparison.OrdinalIgnoreCase) ||
                                         vgName.Contains(vg.Name, StringComparison.OrdinalIgnoreCase)));
                            }
                            
                            if (matchedGroup != null) {
                                vgRef.Id = matchedGroup.Id;
                                vgRef.VariableCount = matchedGroup.Variables?.Count ?? 0;
                                vgRef.ResourceUrl = matchedGroup.ResourceUrl;
                            } else {
                                vgRef.ResourceUrl = $"{projectUrl}/_library?itemType=VariableGroups";
                            }

                            item.VariableGroups.Add(vgRef);
                        }
                    }
                }
            } catch {
                // Could not fetch/parse YAML
            }
        }

        // Fallback: If no variable groups from YAML parsing, try from definition
        if (item.VariableGroups.Count == 0 && fullDef.VariableGroups?.Any() == true) {
            try {
                foreach (var vg in fullDef.VariableGroups) {
                    var vgRef = new DataObjects.PipelineVariableGroupRef {
                        Name = vg.Name ?? "",
                        Id = vg.Id,
                        VariableCount = 0,
                        Environment = null
                    };
                    
                    if (!string.IsNullOrWhiteSpace(vg.Name) && variableGroupsDict.TryGetValue(vg.Name, out var fullVg)) {
                        vgRef.ResourceUrl = fullVg.ResourceUrl;
                        vgRef.VariableCount = fullVg.Variables?.Count ?? 0;
                    } else if (vg.Id > 0) {
                        vgRef.ResourceUrl = $"{projectUrl}/_library?itemType=VariableGroups&view=VariableGroupView&variableGroupId={vg.Id}";
                    } else {
                        vgRef.ResourceUrl = $"{projectUrl}/_library?itemType=VariableGroups";
                    }
                    
                    item.VariableGroups.Add(vgRef);
                }
            } catch {
                // Ignore errors
            }
        }
    }

    public async Task<DataObjects.PipelineRunsResponse> GetPipelineRunsForDashboardAsync(string pat, string orgName, string projectId, int pipelineId, int top = 5, string? connectionId = null)
    {
        var response = new DataObjects.PipelineRunsResponse();

        try {
            using var connection = CreateConnection(pat, orgName);
            var buildClient = connection.GetClient<BuildHttpClient>();

            var builds = await buildClient.GetBuildsAsync(projectId, definitions: [pipelineId], top: top);

            response.Runs = builds.Select(b => {
                dynamic? resource = null;
                string? url = null;
                try {
                    resource = b.Links.Links["web"];
                    url = Uri.EscapeUriString(string.Empty + resource.Href);
                } catch { }

                var runInfo = new DataObjects.PipelineRunInfo {
                    RunId = b.Id,
                    Status = b.Status?.ToString() ?? string.Empty,
                    Result = b.Result?.ToString() ?? string.Empty,
                    StartTime = b.StartTime,
                    FinishTime = b.FinishTime,
                    ResourceUrl = url,
                    SourceBranch = b.SourceBranch,
                    SourceVersion = b.SourceVersion
                };

                // Map trigger information
                MapBuildTriggerInfo(b, runInfo);

                return runInfo;
            }).ToList();

            response.Success = true;
        } catch (Exception ex) {
            response.Success = false;
            response.ErrorMessage = $"Error loading pipeline runs: {ex.Message}";
        }

        return response;
    }

    public async Task<DataObjects.PipelineYamlResponse> GetPipelineYamlContentAsync(string pat, string orgName, string projectId, int pipelineId, string? connectionId = null)
    {
        var response = new DataObjects.PipelineYamlResponse();

        try {
            using var connection = CreateConnection(pat, orgName);
            var buildClient = connection.GetClient<BuildHttpClient>();
            var gitClient = connection.GetClient<GitHttpClient>();

            var definition = await buildClient.GetDefinitionAsync(projectId, pipelineId);
            
            if (definition.Process is YamlProcess yamlProcess && !string.IsNullOrWhiteSpace(yamlProcess.YamlFilename)) {
                var repoId = definition.Repository.Id;
                var branch = definition.Repository.DefaultBranch?.Replace("refs/heads/", "") ?? "main";

                var versionDescriptor = new GitVersionDescriptor {
                    Version = branch,
                    VersionType = GitVersionType.Branch
                };

                var yamlItem = await gitClient.GetItemAsync(
                    project: projectId,
                    repositoryId: repoId,
                    path: yamlProcess.YamlFilename,
                    scopePath: null,
                    recursionLevel: VersionControlRecursionType.None,
                    includeContent: true,
                    versionDescriptor: versionDescriptor);

                response.Yaml = yamlItem?.Content ?? "";
                response.YamlFileName = yamlProcess.YamlFilename;
                response.Success = true;
            } else {
                response.Success = false;
                response.ErrorMessage = "Pipeline does not use YAML process.";
            }
        } catch (Exception ex) {
            response.Success = false;
            response.ErrorMessage = $"Error loading pipeline YAML: {ex.Message}";
        }

        return response;
    }

    public DataObjects.ParsedPipelineSettings ParsePipelineYaml(string yamlContent, int? pipelineId = null, string? pipelineName = null, string? pipelinePath = null)
    {
        var result = new DataObjects.ParsedPipelineSettings {
            PipelineId = pipelineId,
            PipelineName = pipelineName,
            Environments = []
        };

        if (string.IsNullOrWhiteSpace(yamlContent)) {
            return result;
        }

        try {
            var lines = yamlContent.Split('\n');
            var envNames = new HashSet<string> { "DEV", "PROD", "CMS", "STAGING", "QA", "UAT", "TEST" };
            
            // Dictionary to collect environment settings (so we can merge multiple passes)
            var envDict = new Dictionary<string, DataObjects.ParsedEnvironmentSettings>(StringComparer.OrdinalIgnoreCase);

            // Parse BuildRepo information from resources.repositories section
            ExtractBuildRepoInfo(lines, result);
            
            // Copy code repo info to wizard fields for import
            if (!string.IsNullOrWhiteSpace(result.CodeProjectName)) {
                result.ProjectName = result.CodeProjectName;
            }
            if (!string.IsNullOrWhiteSpace(result.CodeRepoName)) {
                result.RepoName = result.CodeRepoName;
            }
            if (!string.IsNullOrWhiteSpace(result.CodeBranch)) {
                result.SelectedBranch = result.CodeBranch;
            }

            // Parse line by line, looking for name/value pairs
            // YAML format: 
            //   - name: CI_BUILD_CsProjectPath
            //     value: "path/to/project.csproj"
            for (int i = 0; i < lines.Length; i++) {
                var trimmed = lines[i].Trim();
                
                // Skip comments and empty lines
                if (trimmed.StartsWith("#") || string.IsNullOrWhiteSpace(trimmed)) {
                    continue;
                }
                
                // Look for "- name: VARNAME" pattern and get value from next line
                if (trimmed.StartsWith("- name:") || trimmed.StartsWith("name:")) {
                    var varName = ExtractYamlValue(trimmed);
                    string varValue = string.Empty;
                    
                    // Look at next line for "value:"
                    if (i + 1 < lines.Length) {
                        var nextLine = lines[i + 1].Trim();
                        if (nextLine.StartsWith("value:")) {
                            varValue = ExtractYamlValue(nextLine);
                        }
                    }
                    
                    // Skip if value is a variable reference
                    if (varValue.StartsWith("$")) {
                        continue;
                    }
                    
                    // Extract CI_BUILD_CsProjectPath
                    if (varName.Equals("CI_BUILD_CsProjectPath", StringComparison.OrdinalIgnoreCase)) {
                        if (!string.IsNullOrWhiteSpace(varValue)) {
                            // Remove leading slash if present
                            result.SelectedCsprojPath = varValue.TrimStart('/', '\\');
                        }
                    }
                    
                    // Extract CI_ProjectName
                    if (varName.Equals("CI_ProjectName", StringComparison.OrdinalIgnoreCase)) {
                        if (!string.IsNullOrWhiteSpace(varValue) && string.IsNullOrWhiteSpace(result.ProjectName)) {
                            result.ProjectName = varValue;
                        }
                    }
                    
                    // Look for environment-specific variables: CI_{ENV}_{Property}
                    foreach (var env in envNames) {
                        // Ensure the environment entry exists
                        if (!envDict.ContainsKey(env)) {
                            envDict[env] = new DataObjects.ParsedEnvironmentSettings {
                                EnvironmentName = env,
                                Confidence = DataObjects.ParseConfidence.Medium
                            };
                        }
                        
                        var envSettings = envDict[env];
                        
                        // Variable group reference: CI_{ENV}_VariableGroup
                        if (varName.Equals($"CI_{env}_VariableGroup", StringComparison.OrdinalIgnoreCase)) {
                            if (!string.IsNullOrWhiteSpace(varValue)) {
                                envSettings.VariableGroupName = varValue;
                                envSettings.Confidence = DataObjects.ParseConfidence.High;
                            }
                        }
                        
                        // IIS Deployment Type: CI_{ENV}_IISDeploymentType
                        if (varName.Equals($"CI_{env}_IISDeploymentType", StringComparison.OrdinalIgnoreCase)) {
                            if (!string.IsNullOrWhiteSpace(varValue)) {
                                envSettings.IISDeploymentType = varValue;
                            }
                        }
                        
                        // Website Name: CI_{ENV}_WebsiteName
                        if (varName.Equals($"CI_{env}_WebsiteName", StringComparison.OrdinalIgnoreCase)) {
                            if (!string.IsNullOrWhiteSpace(varValue)) {
                                envSettings.WebsiteName = varValue;
                            }
                        }
                        
                        // Virtual Path: CI_{ENV}_VirtualPath
                        if (varName.Equals($"CI_{env}_VirtualPath", StringComparison.OrdinalIgnoreCase)) {
                            if (!string.IsNullOrWhiteSpace(varValue)) {
                                envSettings.VirtualPath = varValue;
                            }
                        }
                        
                        // App Pool Name: CI_{ENV}_AppPoolName
                        if (varName.Equals($"CI_{env}_AppPoolName", StringComparison.OrdinalIgnoreCase)) {
                            if (!string.IsNullOrWhiteSpace(varValue)) {
                                envSettings.AppPoolName = varValue;
                            }
                        }
                        
                        // Binding Info: CI_{ENV}_BindingInfo
                        if (varName.Equals($"CI_{env}_BindingInfo", StringComparison.OrdinalIgnoreCase)) {
                            if (!string.IsNullOrWhiteSpace(varValue)) {
                                envSettings.BindingInfo = varValue;
                            }
                        }
                    }
                }
            }
            
            // Only include environments that have actual data (at minimum a variable group or other setting)
            foreach (var kvp in envDict) {
                var env = kvp.Value;
                if (!string.IsNullOrWhiteSpace(env.VariableGroupName) ||
                    !string.IsNullOrWhiteSpace(env.WebsiteName) ||
                    !string.IsNullOrWhiteSpace(env.VirtualPath) ||
                    !string.IsNullOrWhiteSpace(env.AppPoolName)) {
                    result.Environments.Add(env);
                }
            }
            
            // Detect if this is a FreeCICD-generated pipeline
            result.IsFreeCICDGenerated = yamlContent.Contains("CI_BUILD_CsProjectPath", StringComparison.OrdinalIgnoreCase) ||
                                          yamlContent.Contains("TemplateRepo", StringComparison.OrdinalIgnoreCase);
            
        } catch {
            // If parsing fails, return partial result
        }

        return result;
    }
    
    /// <summary>
    /// Extracts the value from a YAML line like "- name: SomeValue" or "value: something"
    /// </summary>
    private string ExtractYamlValue(string line)
    {
        var trimmed = line.Trim();
        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex > 0 && colonIndex < trimmed.Length - 1) {
            return trimmed[(colonIndex + 1)..].Trim().Trim('"', '\'');
        }
        return string.Empty;
    }
    
    /// <summary>
    /// Extracts BuildRepo information from YAML lines.
    /// </summary>
    private void ExtractBuildRepoInfo(string[] lines, DataObjects.ParsedPipelineSettings result)
    {
        bool inBuildRepo = false;
        for (int i = 0; i < lines.Length; i++) {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("- repository:") && trimmed.Contains("BuildRepo", StringComparison.OrdinalIgnoreCase)) {
                inBuildRepo = true;
                continue;
            }
            if (inBuildRepo) {
                if (trimmed.StartsWith("- repository:") || (trimmed.Length > 0 && !char.IsWhiteSpace(lines[i][0]) && !trimmed.StartsWith("-"))) {
                    inBuildRepo = false;
                    continue;
                }
                if (trimmed.StartsWith("name:")) {
                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex > 0 && colonIndex < trimmed.Length - 1) {
                        var value = trimmed[(colonIndex + 1)..].Trim().Trim('"', '\'');
                        var parts = value.Split('/');
                        if (parts.Length >= 2) {
                            result.CodeProjectName = parts[0];
                            result.CodeRepoName = parts[1];
                        } else if (parts.Length == 1 && !string.IsNullOrWhiteSpace(parts[0])) {
                            result.CodeRepoName = parts[0];
                        }
                    }
                }
                if (trimmed.StartsWith("ref:")) {
                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex > 0 && colonIndex < trimmed.Length - 1) {
                        var value = trimmed[(colonIndex + 1)..].Trim().Trim('"', '\'');
                        result.CodeBranch = value.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase) ? value[11..] : value;
                    }
                }
            }
        }
    }

    public async Task<Dictionary<string, DataObjects.IISInfo?>> GetDevOpsIISInfoAsync()
    {
        var result = new Dictionary<string, DataObjects.IISInfo?>();
        
        // IIS info JSON files are generated by deployment pipelines and placed in the app root
        // File naming convention: IISInfo_Azure{Environment}.json
        // Maps to: DEV → AzureDev, PROD → AzureProd, CMS → AzureCMS
        var envMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "DEV", "AzureDev" },
            { "PROD", "AzureProd" },
            { "CMS", "AzureCMS" }
        };
        
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        
        foreach (var mapping in envMappings) {
            var fileName = $"IISInfo_{mapping.Value}.json";
            var filePath = Path.Combine(basePath, fileName);
            
            try {
                if (File.Exists(filePath)) {
                    var jsonContent = await File.ReadAllTextAsync(filePath);
                    var iisInfo = System.Text.Json.JsonSerializer.Deserialize<DataObjects.IISInfo>(
                        jsonContent,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (iisInfo != null) {
                        result[mapping.Key] = iisInfo;
                    }
                }
            } catch {
                // If file doesn't exist or can't be parsed, skip this environment
            }
        }
        
        return result;
    }

    /// <summary>
    /// Maps Azure DevOps Build trigger information to our simplified TriggerType and display fields.
    /// </summary>
    private void MapBuildTriggerInfo(Build build, DataObjects.PipelineListItem item)
    {
        var reason = build.Reason;
        item.TriggerReason = reason.ToString();

        switch (reason) {
            case BuildReason.Manual:
                item.TriggerType = DataObjects.TriggerType.Manual;
                item.TriggerDisplayText = "Manual";
                item.IsAutomatedTrigger = false;
                break;
            case BuildReason.IndividualCI:
            case BuildReason.BatchedCI:
                item.TriggerType = DataObjects.TriggerType.CodePush;
                item.TriggerDisplayText = "Code push";
                item.IsAutomatedTrigger = true;
                break;
            case BuildReason.Schedule:
                item.TriggerType = DataObjects.TriggerType.Scheduled;
                item.TriggerDisplayText = "Scheduled";
                item.IsAutomatedTrigger = true;
                break;
            case BuildReason.PullRequest:
            case BuildReason.ValidateShelveset:
                item.TriggerType = DataObjects.TriggerType.PullRequest;
                item.TriggerDisplayText = "Pull request";
                item.IsAutomatedTrigger = true;
                break;
            case BuildReason.BuildCompletion:
                item.TriggerType = DataObjects.TriggerType.PipelineCompletion;
                item.TriggerDisplayText = "Pipeline completion";
                item.IsAutomatedTrigger = true;
                break;
            case BuildReason.ResourceTrigger:
                item.TriggerType = DataObjects.TriggerType.ResourceTrigger;
                item.TriggerDisplayText = "Resource";
                item.IsAutomatedTrigger = true;
                break;
            default:
                item.TriggerType = DataObjects.TriggerType.Other;
                item.TriggerDisplayText = reason.ToString();
                item.IsAutomatedTrigger = true;
                break;
        }

        if (build.RequestedFor != null) {
            item.TriggeredByUser = build.RequestedFor.DisplayName;
            item.TriggeredByAvatarUrl = build.RequestedFor.ImageUrl;
        } else if (build.RequestedBy != null) {
            item.TriggeredByUser = build.RequestedBy.DisplayName;
            item.TriggeredByAvatarUrl = build.RequestedBy.ImageUrl;
        }

        if (reason == BuildReason.BuildCompletion && build.TriggerInfo != null) {
            try {
                if (build.TriggerInfo.TryGetValue("triggeringBuild.definition.name", out var triggerName)) {
                    item.TriggeredByPipeline = triggerName;
                }
            } catch { }
        }
    }

    /// <summary>
    /// Maps Azure DevOps Build trigger information to PipelineRunInfo.
    /// </summary>
    private void MapBuildTriggerInfo(Build build, DataObjects.PipelineRunInfo runInfo)
    {
        var reason = build.Reason;
        runInfo.TriggerReason = reason.ToString();

        switch (reason) {
            case BuildReason.Manual:
                runInfo.TriggerType = DataObjects.TriggerType.Manual;
                runInfo.TriggerDisplayText = "Manual";
                runInfo.IsAutomatedTrigger = false;
                break;
            case BuildReason.IndividualCI:
            case BuildReason.BatchedCI:
                runInfo.TriggerType = DataObjects.TriggerType.CodePush;
                runInfo.TriggerDisplayText = "Code push";
                runInfo.IsAutomatedTrigger = true;
                break;
            case BuildReason.Schedule:
                runInfo.TriggerType = DataObjects.TriggerType.Scheduled;
                runInfo.TriggerDisplayText = "Scheduled";
                runInfo.IsAutomatedTrigger = true;
                break;
            case BuildReason.PullRequest:
            case BuildReason.ValidateShelveset:
                runInfo.TriggerType = DataObjects.TriggerType.PullRequest;
                runInfo.TriggerDisplayText = "Pull request";
                runInfo.IsAutomatedTrigger = true;
                break ;
            case BuildReason.BuildCompletion:
                runInfo.TriggerType = DataObjects.TriggerType.PipelineCompletion;
                runInfo.TriggerDisplayText = "Pipeline completion";
                runInfo.IsAutomatedTrigger = true;
                break;
            case BuildReason.ResourceTrigger:
                runInfo.TriggerType = DataObjects.TriggerType.ResourceTrigger;
                runInfo.TriggerDisplayText = "Resource";
                runInfo.IsAutomatedTrigger = true;
                break;
            default:
                runInfo.TriggerType = DataObjects.TriggerType.Other;
                runInfo.TriggerDisplayText = reason.ToString();
                runInfo.IsAutomatedTrigger = true;
                break;
        }

        if (build.RequestedFor != null) {
            runInfo.TriggeredByUser = build.RequestedFor.DisplayName;
        } else if (build.RequestedBy != null) {
            runInfo.TriggeredByUser = build.RequestedBy.DisplayName;
        }
    }

    /// <summary>
    /// Queues a new build run for a pipeline definition (equivalent to "Run pipeline" in Azure DevOps).
    /// </summary>
    public async Task<DataObjects.BooleanResponse> RunPipelineAsync(string pat, string orgName, string projectId, int pipelineId)
    {
        var output = new DataObjects.BooleanResponse();

        try {
            using var connection = CreateConnection(pat, orgName);
            var buildClient = connection.GetClient<BuildHttpClient>();

            var definition = await buildClient.GetDefinitionAsync(projectId, pipelineId);

            var build = new Build {
                Definition = new DefinitionReference { Id = definition.Id },
                Project = definition.Project
            };

            var queuedBuild = await buildClient.QueueBuildAsync(build);
            output.Result = true;
            output.Messages.Add($"Pipeline '{definition.Name}' queued successfully (Build #{queuedBuild.BuildNumber}).");
        } catch (Exception ex) {
            output.Result = false;
            output.Messages.Add($"Failed to run pipeline: {ex.Message}");
        }

        return output;
    }

    /// <summary>
    /// Gets stage bubbles (lightweight stage status) for a specific build from the Timeline API.
    /// </summary>
    public async Task<List<DataObjects.StageBubble>> GetBuildStageBubblesAsync(BuildHttpClient buildClient, string projectId, int buildId)
    {
        var output = new List<DataObjects.StageBubble>();

        try {
            var timeline = await buildClient.GetBuildTimelineAsync(projectId, buildId);
            if (timeline?.Records != null) {
                var stages = timeline.Records
                    .Where(r => r.RecordType == "Stage")
                    .OrderBy(r => r.Order)
                    .ToList();

                foreach (var stage in stages) {
                    // Skip internal checkpoint stages
                    if (stage.Name?.StartsWith("__") == true) continue;

                    output.Add(new DataObjects.StageBubble {
                        Name = stage.Name ?? "",
                        State = stage.State?.ToString()?.ToLower() ?? "pending",
                        Result = stage.Result?.ToString()?.ToLower(),
                        Order = stage.Order ?? 0
                    });
                }
            }
        } catch {
            // Timeline not available yet (build queued but not started)
        }

        return output;
    }

    /// <summary>
    /// Gets detailed build timeline (stages + jobs) for the expanded row view.
    /// </summary>
    public async Task<DataObjects.BuildTimelineResponse> GetBuildTimelineAsync(string pat, string orgName, string projectId, int buildId)
    {
        var response = new DataObjects.BuildTimelineResponse { BuildId = buildId };

        try {
            using var connection = CreateConnection(pat, orgName);
            var buildClient = connection.GetClient<BuildHttpClient>();

            var build = await buildClient.GetBuildAsync(projectId, buildId);
            response.BuildNumber = build.BuildNumber;
            response.BuildStatus = build.Status?.ToString();
            response.BuildResult = build.Result?.ToString();

            var timeline = await buildClient.GetBuildTimelineAsync(projectId, buildId);
            if (timeline?.Records != null) {
                var stageRecords = timeline.Records
                    .Where(r => r.RecordType == "Stage" && r.Name?.StartsWith("__") != true)
                    .OrderBy(r => r.Order)
                    .ToList();

                var jobRecords = timeline.Records
                    .Where(r => r.RecordType == "Job")
                    .ToList();

                var taskRecords = timeline.Records
                    .Where(r => r.RecordType == "Task")
                    .ToList();

                var baseUrl = $"https://dev.azure.com/{orgName}";

                foreach (var stage in stageRecords) {
                    var stageInfo = new DataObjects.BuildStageInfo {
                        Id = stage.Id.ToString(),
                        Name = stage.Name ?? "",
                        Order = stage.Order ?? 0,
                        State = stage.State?.ToString()?.ToLower() ?? "pending",
                        Result = stage.Result?.ToString()?.ToLower(),
                        StartTime = stage.StartTime,
                        FinishTime = stage.FinishTime
                    };

                    // Find jobs belonging to this stage
                    var stageJobs = jobRecords
                        .Where(j => j.ParentId == stage.Id)
                        .OrderBy(j => j.Order)
                        .ToList();

                    foreach (var job in stageJobs) {
                        var jobTasks = taskRecords.Where(t => t.ParentId == job.Id).ToList();

                        stageInfo.Jobs.Add(new DataObjects.BuildJobInfo {
                            Id = job.Id.ToString(),
                            Name = job.Name ?? "",
                            Order = job.Order ?? 0,
                            State = job.State?.ToString()?.ToLower() ?? "pending",
                            Result = job.Result?.ToString()?.ToLower(),
                            StartTime = job.StartTime,
                            FinishTime = job.FinishTime,
                            TaskCount = jobTasks.Count,
                            TasksCompleted = jobTasks.Count(t => t.State?.ToString()?.ToLower() == "completed"),
                            LogUrl = job.Log?.Url
                        });
                    }

                    response.Stages.Add(stageInfo);
                }

                // Compute dependency columns from timing
                ComputeStageColumns(response.Stages);
            }

            response.Success = true;
        } catch (Exception ex) {
            response.Success = false;
            response.ErrorMessage = $"Error loading build timeline: {ex.Message}";
        }

        return response;
    }

    /// <summary>
    /// Computes dependency column assignments for stages based on start time proximity.
    /// Stages that start within 5 seconds of each other are parallel (same column).
    /// For stages without start times (pending/not started), each gets its own column after the last timed column.
    /// </summary>
    private static void ComputeStageColumns(List<DataObjects.BuildStageInfo> stages)
    {
        if (stages.Count == 0) return;

        // Separate stages with timing data from those without
        var timedStages = stages
            .Where(s => s.StartTime.HasValue)
            .OrderBy(s => s.StartTime!.Value)
            .ToList();

        var untimedStages = stages
            .Where(s => !s.StartTime.HasValue)
            .OrderBy(s => s.Order)
            .ToList();

        int currentColumn = 0;

        if (timedStages.Count > 0) {
            // First timed stage gets column 0
            timedStages[0].Column = 0;
            var columnStartTime = timedStages[0].StartTime!.Value;

            for (int i = 1; i < timedStages.Count; i++) {
                var stage = timedStages[i];
                var timeDiff = (stage.StartTime!.Value - columnStartTime).TotalSeconds;

                if (timeDiff <= 5) {
                    // Within 5 seconds → same column (parallel)
                    stage.Column = currentColumn;
                } else {
                    // New column
                    currentColumn++;
                    stage.Column = currentColumn;
                    columnStartTime = stage.StartTime!.Value;
                }
            }

            currentColumn++;
        }

        // Untimed stages each get their own column after the timed ones
        foreach (var stage in untimedStages) {
            stage.Column = currentColumn++;
        }
    }

    /// <summary>
    /// Gets log content for a specific build job from the Timeline API.
    /// </summary>
    public async Task<DataObjects.BuildLogResponse> GetBuildJobLogsAsync(string pat, string orgName, string projectId, int buildId, string jobId)
    {
        var response = new DataObjects.BuildLogResponse { BuildId = buildId };

        try {
            using var connection = CreateConnection(pat, orgName);
            var buildClient = connection.GetClient<BuildHttpClient>();

            // Get the timeline to find the log ID for this job
            var timeline = await buildClient.GetBuildTimelineAsync(projectId, buildId);
            if (timeline?.Records == null) {
                response.ErrorMessage = "Timeline not available";
                return response;
            }

            // Find the job record
            var jobRecord = timeline.Records.FirstOrDefault(r =>
                r.Id.ToString() == jobId && r.RecordType == "Job");

            if (jobRecord == null) {
                response.ErrorMessage = "Job not found in timeline";
                return response;
            }

            response.JobName = jobRecord.Name ?? "";

            // Get task records (children of this job) that have logs
            var taskRecords = timeline.Records
                .Where(r => r.ParentId == jobRecord.Id && r.Log != null)
                .OrderBy(r => r.Order)
                .ToList();

            int lineNumber = 1;

            foreach (var task in taskRecords) {
                if (task.Log?.Id == null) continue;

                try {
                    var logLines = await buildClient.GetBuildLogLinesAsync(projectId, buildId, task.Log.Id);
                    if (logLines != null) {
                        // Add a section header for each task
                        response.Lines.Add(new DataObjects.BuildLogLine {
                            LineNumber = lineNumber++,
                            Text = $"══════ {task.Name} ══════",
                            Severity = "section"
                        });

                        foreach (var line in logLines) {
                            var severity = "info";
                            if (line.Contains("##[error]")) severity = "error";
                            else if (line.Contains("##[warning]")) severity = "warning";
                            else if (line.Contains("##[section]")) severity = "section";

                            response.Lines.Add(new DataObjects.BuildLogLine {
                                LineNumber = lineNumber++,
                                Text = line
                                    .Replace("##[error]", "")
                                    .Replace("##[warning]", "")
                                    .Replace("##[section]", "")
                                    .TrimStart(),
                                Severity = severity
                            });
                        }
                    }
                } catch {
                    response.Lines.Add(new DataObjects.BuildLogLine {
                        LineNumber = lineNumber++,
                        Text = $"(Could not load logs for {task.Name})",
                        Severity = "warning"
                    });
                }
            }

            response.Success = true;
        } catch (Exception ex) {
            response.ErrorMessage = $"Error loading build logs: {ex.Message}";
        }

        return response;
    }

    /// <summary>
    /// Gets org-wide pipeline health trends by fetching recent builds for all pipelines.
    /// </summary>
    public async Task<DataObjects.OrgHealthResponse> GetOrgHealthAsync(string pat, string orgName, string projectId, int buildsPerPipeline = 10)
    {
        var response = new DataObjects.OrgHealthResponse();

        try {
            using var connection = CreateConnection(pat, orgName);
            var buildClient = connection.GetClient<BuildHttpClient>();

            // Get all pipeline definitions
            var definitions = await buildClient.GetDefinitionsAsync(projectId);

            int totalSucceeded = 0;
            int totalFailed = 0;
            int totalBuilds = 0;

            var semaphore = new SemaphoreSlim(5, 5);
            var tasks = definitions.Select(async def => {
                await semaphore.WaitAsync();
                try {
                    var builds = await buildClient.GetBuildsAsync(projectId,
                        definitions: [def.Id],
                        top: buildsPerPipeline);

                    var results = builds
                        .Where(b => b.Status?.ToString()?.ToLower() != "inprogress")
                        .Select(b => b.Result?.ToString()?.ToLower() ?? "unknown")
                        .ToList();

                    // Also include in-progress builds
                    var inProgress = builds
                        .Where(b => b.Status?.ToString()?.ToLower() == "inprogress")
                        .Select(_ => "inprogress")
                        .ToList();

                    var allResults = inProgress.Concat(results).Take(buildsPerPipeline).ToList();

                    int succeeded = results.Count(r => r == "succeeded");
                    int failed = results.Count(r => r == "failed");
                    int total = results.Count;
                    int successRate = total > 0 ? (int)Math.Round(succeeded * 100.0 / total) : 0;

                    // Calculate streak
                    int streak = 0;
                    if (results.Count > 0) {
                        var first = results.FirstOrDefault() ?? "";
                        if (first == "succeeded") {
                            streak = results.TakeWhile(r => r == "succeeded").Count();
                        } else if (first == "failed") {
                            streak = -results.TakeWhile(r => r == "failed").Count();
                        }
                    }

                    return new DataObjects.PipelineHealthTrend {
                        PipelineId = def.Id,
                        Name = def.Name ?? "",
                        RecentResults = allResults,
                        SuccessRate = successRate,
                        Streak = streak
                    };
                } finally {
                    semaphore.Release();
                }
            }).ToList();

            var trends = await Task.WhenAll(tasks);
            response.Pipelines = trends.OrderBy(t => t.SuccessRate).ThenBy(t => t.Name).ToList();

            foreach (var t in trends) {
                var completedResults = t.RecentResults.Where(r => r != "inprogress").ToList();
                totalSucceeded += completedResults.Count(r => r == "succeeded");
                totalFailed += completedResults.Count(r => r == "failed");
                totalBuilds += completedResults.Count;
            }

            response.Summary = new DataObjects.OrgHealthSummary {
                TotalPipelines = trends.Length,
                HealthyPipelines = trends.Count(t => t.SuccessRate >= 80),
                FailingPipelines = trends.Count(t => t.SuccessRate < 50 && t.RecentResults.Any()),
                UnstablePipelines = trends.Count(t => t.SuccessRate >= 50 && t.SuccessRate < 80),
                OverallHealthPercent = totalBuilds > 0 ? (int)Math.Round(totalSucceeded * 100.0 / totalBuilds) : 0,
                TotalBuildsAnalyzed = totalBuilds,
                TotalSucceeded = totalSucceeded,
                TotalFailed = totalFailed
            };

            response.Success = true;
        } catch (Exception ex) {
            response.ErrorMessage = $"Error loading org health: {ex.Message}";
        }

        return response;
    }
}

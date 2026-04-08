using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;

namespace FreeCICD;

// Pipeline Operations: CRUD + YAML generation

public partial class DataAccess
{
    public async Task<List<DataObjects.DevOpsBuild>> GetPipelineRuns(int pipelineId, string projectId, string pat, string orgName, int skip = 0, int top = 10, string? connectionId = null)
    {
        using (var connection = CreateConnection(pat, orgName)) {
            try {
                var buildClient = connection.GetClient<BuildHttpClient>();
                var builds = await buildClient.GetBuildsAsync(projectId, definitions: new List<int> { pipelineId });
                var pagedBuilds = builds.Skip(skip).Take(top).ToList();
                var devOpsBuilds = pagedBuilds.Select(b => {
                    dynamic resource = b.Links.Links["web"];
                    var url = Uri.EscapeUriString(string.Empty + resource.Href);

                    var item = new DataObjects.DevOpsBuild {
                        Id = b.Id,
                        Status = b.Status.ToString() ?? string.Empty,
                        Result = b.Result.HasValue ? b.Result.Value.ToString() : "",
                        QueueTime = b?.QueueTime ?? DateTime.UtcNow,
                        ResourceUrl = url
                    };

                    return item;
                }).ToList();
                return devOpsBuilds;
            } catch (Exception ex) {
                throw new Exception($"Error getting pipeline runs: {ex.Message}");
            }
        }
    }

    public async Task<DataObjects.DevopsPipelineDefinition> GetDevOpsPipeline(string projectId, int pipelineId, string pat, string orgName, string? connectionId = null)
    {
        var output = new DataObjects.DevopsPipelineDefinition();

        if (!string.IsNullOrWhiteSpace(connectionId)) {
            await SignalRUpdate(new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                ConnectionId = connectionId,
                ItemId = Guid.NewGuid(),
                Message = "Start of lookup of pipeline"
            });
        }

        if (pipelineId == 0) {
            output.Name = "No pipeline yet";
        } else {
            using (var connection = CreateConnection(pat, orgName)) {
                try {
                    var buildClient = connection.GetClient<BuildHttpClient>();

                    var pipelineDefinition = await buildClient.GetDefinitionAsync(projectId, pipelineId);
                    dynamic pipelineReferenceLink = pipelineDefinition.Links.Links["web"];
                    var pipelineUrl = Uri.EscapeUriString(string.Empty + pipelineReferenceLink.Href);
                    string yamlFilename = string.Empty;
                    if (pipelineDefinition.Process is YamlProcess yamlProcess) {
                        yamlFilename = yamlProcess.YamlFilename;
                    }

                    var pipeline = new DataObjects.DevopsPipelineDefinition {
                        Id = pipelineId,
                        Name = pipelineDefinition?.Name ?? string.Empty,
                        QueueStatus = pipelineDefinition?.QueueStatus.ToString() ?? string.Empty,
                        YamlFileName = yamlFilename,
                        Path = pipelineDefinition?.Repository?.Name ?? string.Empty,
                        RepoGuid = pipelineDefinition?.Repository?.Id.ToString() ?? string.Empty,
                        RepositoryName = pipelineDefinition?.Repository?.Name ?? string.Empty,
                        DefaultBranch = pipelineDefinition?.Repository?.DefaultBranch ?? string.Empty,
                        ResourceUrl = pipelineUrl
                    };

                    if (!string.IsNullOrWhiteSpace(connectionId)) {
                        await SignalRUpdate(new DataObjects.SignalRUpdate {
                            UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                            ConnectionId = connectionId,
                            ItemId = Guid.NewGuid(),
                            Message = $"Found pipeline {pipeline.Name}"
                        });
                    }
                    output = pipeline;
                } catch (Exception ex) {
                    throw new Exception($"Error retrieving pipeline: {ex.Message}");
                }
            }
        }
        return output;
    }

    public async Task<List<DataObjects.DevopsPipelineDefinition>> GetDevOpsPipelines(string projectId, string pat, string orgName, string? connectionId = null)
    {
        using (var connection = CreateConnection(pat, orgName)) {
            try {
                var buildClient = connection.GetClient<BuildHttpClient>();
                var definitions = await buildClient.GetDefinitionsAsync(project: projectId);
                var pipelines = new List<DataObjects.DevopsPipelineDefinition>();

                if (!string.IsNullOrWhiteSpace(connectionId)) {
                    await SignalRUpdate(new DataObjects.SignalRUpdate {
                        UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                        ConnectionId = connectionId,
                        ItemId = Guid.NewGuid(),
                        Message = "Start of lookup"
                    });
                }

                foreach (var defRef in definitions) {
                    try {
                        var fullDef = await buildClient.GetDefinitionAsync(projectId, defRef.Id);
                        dynamic pipelineReferenceLink = fullDef.Links.Links["web"];
                        var pipelineUrl = Uri.EscapeUriString(string.Empty + pipelineReferenceLink.Href);
                        string yamlFilename = string.Empty;
                        if (fullDef.Process is YamlProcess yamlProcess) {
                            yamlFilename = yamlProcess.YamlFilename;
                        }

                        var pipeline = new DataObjects.DevopsPipelineDefinition {
                            Id = defRef.Id,
                            Name = defRef?.Name ?? string.Empty,
                            QueueStatus = defRef?.QueueStatus.ToString() ?? string.Empty,
                            YamlFileName = yamlFilename,
                            Path = defRef?.Path ?? string.Empty,
                            RepoGuid = fullDef?.Repository?.Id.ToString() ?? string.Empty,
                            RepositoryName = fullDef?.Repository?.Name ?? string.Empty,
                            DefaultBranch = fullDef?.Repository?.DefaultBranch ?? string.Empty,
                            ResourceUrl = pipelineUrl
                        };

                        if (!string.IsNullOrWhiteSpace(connectionId)) {
                            await SignalRUpdate(new DataObjects.SignalRUpdate {
                                UpdateType = DataObjects.SignalRUpdateType.LoadingDevOpsInfoStatusUpdate,
                                ConnectionId = connectionId,
                                ItemId = Guid.NewGuid(),
                                Message = $"Found pipeline {pipeline.Name}"
                            });
                        }

                        pipelines.Add(pipeline);
                    } catch (Exception) {
                        // Error retrieving full definition
                    }
                }
                return pipelines;
            } catch (Exception ex) {
                throw new Exception($"Error getting pipelines: {ex.Message}");
            }
        }
    }

    private DataObjects.BuildDefinition MapBuildDefinition(Microsoft.TeamFoundation.Build.WebApi.BuildDefinition src)
    {
        dynamic resource = src.Links.Links["web"];
        var url = Uri.EscapeUriString(string.Empty + resource.Href);

        return new DataObjects.BuildDefinition {
            Id = src.Id,
            Name = src.Name ?? "",
            QueueStatus = src.QueueStatus.ToString() ?? "",
            YamlFileName = (src.Process is YamlProcess yp ? yp.YamlFilename : ""),
            RepoGuid = src.Repository?.Id.ToString() ?? "",
            RepositoryName = src.Repository?.Name ?? "",
            DefaultBranch = src.Repository?.DefaultBranch ?? "",
            ResourceUrl = url
        };
    }

    public async Task<string> GenerateYmlFileContents(string devopsProjectId, string devopsRepoId, string devopsBranch, int? devopsPipelineId, string? devopsPipelineName, string codeProjectId, string codeRepoId, string codeBranchName, string codeCsProjectFile, Dictionary<GlobalSettings.EnvironmentType, DataObjects.EnvSetting> environmentSettings, string pat, string orgName, string? connectionId = null)
    {
        string output = GlobalSettings.App.BuildPipelineTemplate;
        var devopsProject = await GetDevOpsProjectAsync(pat, orgName, devopsProjectId);
        var devospPipeline = await GetDevOpsPipeline(devopsProjectId, devopsPipelineId ?? 0, pat, orgName);

        var codeProject = await GetDevOpsProjectAsync(pat, orgName, codeProjectId);
        var codeRepo = await GetDevOpsRepoAsync(pat, orgName, codeProjectId, codeRepoId);
        var codeBranch = await GetDevOpsBranchAsync(pat, orgName, codeProjectId, codeRepoId, codeBranchName);

        var pipelineVariables = await GeneratePipelineVariableReplacementText(codeProject.ProjectName, codeCsProjectFile, environmentSettings);
        var deployStages = await GeneratePipelineDeployStagesReplacementText(environmentSettings);

        output = output.Replace("{{DEVOPS_PROJECTNAME}}", $"{devopsProject.ProjectName}");
        output = output.Replace("{{DEVOPS_REPO_BRANCH}}", $"{devopsBranch}");
        output = output.Replace("{{CODE_PROJECT_NAME}}", $"{codeProject.ProjectName}");
        output = output.Replace("{{CODE_REPO_NAME}}", $"{codeRepo.RepoName}");
        output = output.Replace("{{CODE_REPO_BRANCH}}", $"{codeBranch.BranchName}");
        output = output.Replace("{{PIPELINE_VARIABLES}}", $"{pipelineVariables}");
        output = output.Replace("{{PIPELINE_POOL}}", GlobalSettings.App.BuildPiplelinePool);
        output = output.Replace("{{DEPLOY_STAGES}}", $"{deployStages}");

        return output;
    }

    public async Task<string> GeneratePipelineVariableReplacementText(string projectName, string csProjectFile, Dictionary<GlobalSettings.EnvironmentType, DataObjects.EnvSetting> environmentSettings)
    {
        string output = string.Empty;
        
        // Trim leading slashes from csproj path to prevent path issues
        var cleanCsProjectFile = (csProjectFile ?? "").TrimStart('/', '\\');

        var variableDictionary = new Dictionary<string, string>() {
            { "CI_ProjectName", projectName ?? "" },
            { "CI_BUILD_CsProjectPath", cleanCsProjectFile },
            { "CI_BUILD_Namespace", "" }
        };
        var sb = new System.Text.StringBuilder();
        foreach (var kv in variableDictionary) {
            sb.AppendLine($"  - name: {kv.Key}");
            sb.AppendLine($"    value: \"{kv.Value}\"");
        }

        string authUsername = String.Empty;

        foreach (var envKey in GlobalSettings.App.EnviormentTypeOrder) {
            if (environmentSettings.ContainsKey(envKey)) {
                var env = environmentSettings[envKey];
                sb.AppendLine("");
                sb.AppendLine($"# Environment: {env.EnvName}");
                sb.AppendLine($"  - name: CI_{envKey}_IISDeploymentType");
                sb.AppendLine($"    value: \"{env.IISDeploymentType}\"");
                sb.AppendLine($"  - name: CI_{envKey}_WebsiteName");
                sb.AppendLine($"    value: \"{env.WebsiteName}\"");
                sb.AppendLine($"  - name: CI_{envKey}_VirtualPath");
                sb.AppendLine($"    value: \"{env.VirtualPath}\"");
                sb.AppendLine($"  - name: CI_{envKey}_AppPoolName");
                sb.AppendLine($"    value: \"{env.AppPoolName}\"");
                sb.AppendLine($"  - name: CI_{envKey}_VariableGroup");
                sb.AppendLine($"    value: \"{env.VariableGroupName}\"");
                if (!string.IsNullOrWhiteSpace(env.BindingInfo)) {
                    sb.AppendLine($"  - name: CI_{envKey}_BindingInfo");
                    sb.AppendLine($"    value: >");
                    sb.AppendLine($"      {env.BindingInfo}");
                }

                if (String.IsNullOrEmpty(authUsername) && !String.IsNullOrWhiteSpace(env.AuthUser)) {
                    authUsername = env.AuthUser;
                }
            }
        }

        if (!String.IsNullOrWhiteSpace(authUsername)) {
            sb.AppendLine("");
            sb.AppendLine("# username used for app pool configuration and/or to set file and folder permissions.");
            sb.AppendLine("  - name: CI_AuthUsername");
            sb.AppendLine("    value: \"" + authUsername + "\"");
        }
        output = sb.ToString();

        await Task.CompletedTask;
        return output;
    }

    public async Task<string> GeneratePipelineDeployStagesReplacementText(Dictionary<GlobalSettings.EnvironmentType, DataObjects.EnvSetting> environmentSettings)
    {
        string output = string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (var envKey in GlobalSettings.App.EnviormentTypeOrder) {
            if (environmentSettings.ContainsKey(envKey)) {
                var env = environmentSettings[envKey];
                var envSetting = GlobalSettings.App.EnvironmentOptions[envKey];

                string basePath = $"$(CI_PIPELINE_COMMON_ApplicationFolder_{env.EnvName.ToString()})";
                string dotNetVersion = $"$(CI_PIPELINE_COMMON_DotNetVersion_{env.EnvName.ToString()})";
                string appPoolIdentity = $"$(CI_PIPELINE_COMMON_AppPoolIdentity_{env.EnvName.ToString()})";

                sb.AppendLine($"  - stage: Deploy{env.EnvName.ToString()}Stage");
                sb.AppendLine($"    displayName: \"Deploy to {env.EnvName.ToString()}\"");
                sb.AppendLine($"    dependsOn: InfoStage");
                sb.AppendLine($"    variables:");
                sb.AppendLine($"      - group: ${{{{ variables.CI_{envKey}_VariableGroup }}}}");
                sb.AppendLine($"    jobs:");
                sb.AppendLine($"      - deployment: Deploy{env.EnvName.ToString()}");
                sb.AppendLine($"        workspace:");
                sb.AppendLine($"          clean: all");
                sb.AppendLine($"        displayName: \"Deploy to {env.EnvName.ToString()} (Environment-based)\"");
                sb.AppendLine($"        environment:");
                sb.AppendLine($"          name: \"{envSetting.AgentPool}\"");
                sb.AppendLine($"          resourceType: \"VirtualMachine\"");
                sb.AppendLine($"        strategy:");
                sb.AppendLine($"          runOnce:");
                sb.AppendLine($"            deploy:");
                sb.AppendLine($"              steps:");
                sb.AppendLine($"                - checkout: none");
                sb.AppendLine($"                - template: Templates/dump-env-variables-template.yml@TemplateRepo");
                sb.AppendLine($"                - template: Templates/deploy-template.yml@TemplateRepo");
                sb.AppendLine($"                  parameters:");
                sb.AppendLine($"                    envFolderName: \"{env.EnvName}\"");
                sb.AppendLine($"                    basePath: \"{basePath}\"");
                sb.AppendLine($"                    projectName: \"$(CI_ProjectName)\"");
                sb.AppendLine($"                    releaseRetention: \"$(CI_PIPELINE_COMMON_ReleaseRetention)\"");
                sb.AppendLine($"                    IISDeploymentType: \"$(CI_{env.EnvName.ToString()}_IISDeploymentType)\"");
                sb.AppendLine($"                    WebsiteName: \"$(CI_{env.EnvName.ToString()}_WebsiteName)\"");
                sb.AppendLine($"                    VirtualPath: \"$(CI_{env.EnvName.ToString()}_VirtualPath)\"");
                sb.AppendLine($"                    AppPoolName: \"$(CI_{env.EnvName.ToString()}_AppPoolName)\"");
                sb.AppendLine($"                    DotNetVersion: \"{dotNetVersion}\"");
                sb.AppendLine($"                    AppPoolIdentity: \"{appPoolIdentity}\"");
                if (!string.IsNullOrWhiteSpace(env.BindingInfo)) {
                    sb.AppendLine($"                    CustomBindings: \"$(CI_{env.EnvName.ToString()}_BindingInfo)\"");
                }
                sb.AppendLine($"                - template: Templates/clean-workspace-template.yml@TemplateRepo");
                sb.AppendLine();
            }
        }
        output = sb.ToString();
        await Task.CompletedTask;
        return output;
    }

    public async Task<DataObjects.BuildDefinition> CreateOrUpdateDevopsPipeline(string devopsProjectId, string devopsRepoId, string devopsBranchName, int? devopsPipelineId, string? devopsPipelineName, string? devopsPipelineYmlFileName, string codeProjectId, string codeRepoId, string codeBranchName, string codeCsProjectFile, Dictionary<GlobalSettings.EnvironmentType, DataObjects.EnvSetting> environmentSettings, string pat, string orgName, string? connectionId = null)
    {
        DataObjects.BuildDefinition output = new DataObjects.BuildDefinition();
        try {
            var devopsProject = await GetDevOpsProjectAsync(pat, orgName, devopsProjectId);
            var devopsPipeline = await GetDevOpsPipeline(devopsProjectId, devopsPipelineId ?? 0, pat, orgName);
            var devopsRepo = await GetDevOpsRepoAsync(pat, orgName, devopsProjectId, devopsRepoId);
            var devopsBranch = await GetDevOpsBranchAsync(pat, orgName, devopsProjectId, devopsRepoId, devopsBranchName);

            var codeProject = await GetDevOpsProjectAsync(pat, orgName, codeProjectId);
            var codeRepo = await GetDevOpsRepoAsync(pat, orgName, codeProjectId, codeRepoId);
            var codeBranch = await GetDevOpsBranchAsync(pat, orgName, codeProjectId, codeRepoId, codeBranchName);

            List<DataObjects.DevopsVariableGroup> variableGroups = new List<DataObjects.DevopsVariableGroup>();
            var projectVariableGroups = await GetProjectVariableGroupsAsync(pat, orgName, devopsProjectId, connectionId);

            if (devopsPipelineId.HasValue && devopsPipelineId.Value > 0 && string.IsNullOrWhiteSpace(devopsPipelineName)) {
                devopsPipelineName = devopsPipeline.Name;
            }

            foreach (var envKey in GlobalSettings.App.EnviormentTypeOrder) {
                if (environmentSettings.ContainsKey(envKey)) {
                    var env = environmentSettings[envKey];
                    var existing = projectVariableGroups.SingleOrDefault(g => (string.Empty + g.Name).Trim().ToLower() == (string.Empty + env.VariableGroupName).Trim().ToLower());
                    if (existing != null) {
                        variableGroups.Add(existing);
                    } else {
                        var newVariableGroup = await CreateVariableGroup(devopsProjectId, pat, orgName, new DataObjects.DevopsVariableGroup {
                            Name = env.VariableGroupName,
                            Description = $"Variable group for project {codeProject.ProjectName}",
                            Variables = new List<DataObjects.DevopsVariable> {
                                new DataObjects.DevopsVariable {
                                    Name = $"BasePath",
                                    Value = env.VirtualPath,
                                    IsSecret = false,
                                    IsReadOnly = false
                                },
                                new DataObjects.DevopsVariable {
                                    Name = $"ConnectionStrings.AppData",
                                    Value = $"Data Source=localhost;Initial Catalog={devopsProject.ProjectName};TrustServerCertificate=True;Integrated Security=true;MultipleActiveResultSets=True;",
                                    IsSecret = false,
                                    IsReadOnly = false
                                },
                                new DataObjects.DevopsVariable {
                                    Name = $"LocalModelUrl",
                                    Value = string.Empty,
                                    IsSecret = false,
                                    IsReadOnly = false
                                }
                            }
                        });
                    }
                }
            }

            string ymlFileContents = await GenerateYmlFileContents(devopsProjectId, devopsRepoId, devopsBranchName, devopsPipelineId, devopsPipelineName, codeProjectId, codeRepoId, codeBranchName, codeCsProjectFile, environmentSettings, pat, orgName);

            var devopsPipelinePath = $"Projects/{codeProject.ProjectName}";

            var devopsYmlFilePath = devopsPipelineYmlFileName;
            if (string.IsNullOrWhiteSpace(devopsYmlFilePath)) {
                devopsYmlFilePath = $"Projects/{codeProject.ProjectName}/{devopsPipelineName}.yml";
            }

            await CreateOrUpdateGitFile(devopsProject.ProjectId, devopsRepo.RepoId, devopsBranch.BranchName, devopsYmlFilePath, $"{ymlFileContents}", pat, orgName, connectionId);

            string ymlFilePathTrimmed = (string.Empty + devopsYmlFilePath).TrimStart('/', '\\');
            using (var connection = CreateConnection(pat, orgName)) {
                var agentClient = connection.GetClient<TaskAgentHttpClient>();

                var allQueues = await agentClient.GetAgentQueuesAsync(project: new Guid(devopsProjectId));
                var agentPool = allQueues
                    .First(q => q.Name.Equals(GlobalSettings.App.BuildPiplelinePool, StringComparison.OrdinalIgnoreCase));
                var agentPoolQueue = new AgentPoolQueue {
                    Id = agentPool.Id,
                    Name = agentPool.Name
                };

                if (devopsPipelineId > 0) {
                    try {
                        var buildClient = connection.GetClient<BuildHttpClient>();

                        var fullDefinition = await buildClient.GetDefinitionAsync(devopsProjectId, devopsPipelineId.Value);

                        fullDefinition.Triggers?.Clear();

                        var trigger = new ContinuousIntegrationTrigger {
                            SettingsSourceType = 2,
                            BatchChanges = true,
                            MaxConcurrentBuildsPerBranch = 1
                        };

                        fullDefinition.Triggers?.Add(trigger);

                        fullDefinition.Repository.Id = devopsRepoId;
                        fullDefinition.Repository.DefaultBranch = devopsBranchName;
                        fullDefinition.Repository.Type = "TfsGit";

                        fullDefinition.Queue = agentPoolQueue;
                        fullDefinition.QueueStatus = DefinitionQueueStatus.Enabled;

                        fullDefinition.Repository.Properties[RepositoryProperties.CleanOptions] =
                            ((int)RepositoryCleanOptions.AllBuildDir).ToString();

                        fullDefinition.Repository.Properties[RepositoryProperties.FetchDepth] = "1";

                        var result = await buildClient.UpdateDefinitionAsync(fullDefinition, devopsProjectId);
                        output = MapBuildDefinition(result);
                    } catch (Exception ex) {
                        throw new Exception($"Error updating pipeline: {ex.Message}");
                    }
                } else {
                    try {
                        var buildClient = connection.GetClient<BuildHttpClient>();

                        var definition = new Microsoft.TeamFoundation.Build.WebApi.BuildDefinition {
                            Name = devopsPipelineName,
                            Path = devopsPipelinePath,
                            Queue = agentPoolQueue,
                            Project = new TeamProjectReference {
                                Id = new Guid(devopsProject.ProjectId),
                            },
                            Repository = new BuildRepository {
                                Id = devopsRepo.RepoId,
                                Type = "TfsGit",
                                DefaultBranch = devopsBranch.BranchName,
                            },
                            Process = new YamlProcess { YamlFilename = ymlFilePathTrimmed },
                            QueueStatus = DefinitionQueueStatus.Enabled
                        };

                        definition.Repository.Properties[RepositoryProperties.CleanOptions] =
                            ((int)RepositoryCleanOptions.AllBuildDir).ToString();

                        definition.Repository.Properties[RepositoryProperties.FetchDepth] = "1";

                        var trigger = new ContinuousIntegrationTrigger {
                            SettingsSourceType = 2,
                            BatchChanges = true,
                            MaxConcurrentBuildsPerBranch = 1
                        };

                        definition.Triggers.Add(trigger);

                        var createdDefinition = await buildClient.CreateDefinitionAsync(definition);
                        output = MapBuildDefinition(createdDefinition);
                    } catch (Exception ex) {
                        throw new Exception($"Error creating pipeline: {ex.Message}");
                    }
                }
            }
            output.YmlFileContents = await GetGitFile(devopsYmlFilePath, devopsProjectId, devopsRepoId, devopsBranchName, pat, orgName, connectionId);
            return output;
        } catch (Exception) {
            // Error creating or updating DevOps pipeline
        }
        return output;
    }
}

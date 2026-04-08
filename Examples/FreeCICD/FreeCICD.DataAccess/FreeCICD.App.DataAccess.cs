using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace FreeCICD;

// FreeCICD-specific data access methods for Azure DevOps integration
// Implementation split across partial files:
//   - FreeCICD.App.DataAccess.DevOps.Resources.cs   (Projects, Repos, Branches, Variable Groups)
//   - FreeCICD.App.DataAccess.DevOps.GitFiles.cs    (Git file operations)
//   - FreeCICD.App.DataAccess.DevOps.Pipelines.cs   (Pipeline CRUD + YAML generation)
//   - FreeCICD.App.DataAccess.DevOps.Dashboard.cs   (Dashboard operations)
//   - FreeCICD.App.DataAccess.Import.Validation.cs  (Import validation)
//   - FreeCICD.App.DataAccess.Import.Operations.cs  (Import execution)

public partial interface IDataAccess
{
    // DevOps Resource Methods
    Task<DataObjects.DevopsGitRepoBranchInfo> GetDevOpsBranchAsync(string pat, string orgName, string projectId, string repoId, string branchName, string? connectionId = null);
    Task<List<DataObjects.DevopsGitRepoBranchInfo>> GetDevOpsBranchesAsync(string pat, string orgName, string projectId, string repoId, string? connectionId = null);
    Task<List<DataObjects.DevopsFileItem>> GetDevOpsFilesAsync(string pat, string orgName, string projectId, string repoId, string branchName, string? connectionId = null);
    Task<DataObjects.DevopsProjectInfo> GetDevOpsProjectAsync(string pat, string orgName, string projectId, string? connectionId = null);
    Task<List<DataObjects.DevopsProjectInfo>> GetDevOpsProjectsAsync(string pat, string orgName, string? connectionId = null);
    Task<DataObjects.DevopsGitRepoInfo> GetDevOpsRepoAsync(string pat, string orgName, string projectId, string repoId, string? connectionId = null);
    Task<List<DataObjects.DevopsGitRepoInfo>> GetDevOpsReposAsync(string pat, string orgName, string projectId, string? connectionId = null);
    Task<List<DataObjects.DevopsVariableGroup>> GetProjectVariableGroupsAsync(string pat, string orgName, string projectId, string? connectionId = null);
    Task<DataObjects.DevopsVariableGroup> CreateVariableGroup(string projectId, string pat, string orgName, DataObjects.DevopsVariableGroup newGroup, string? connectionId = null);
    Task<DataObjects.DevopsVariableGroup> UpdateVariableGroup(string projectId, string pat, string orgName, DataObjects.DevopsVariableGroup updatedGroup, string? connectionId = null);

    // Git File Methods
    Task<DataObjects.GitUpdateResult> CreateOrUpdateGitFile(string projectId, string repoId, string branch, string filePath, string fileContent, string pat, string orgName, string? connectionId = null);
    Task<DataObjects.GitUpdateResult> CreateOrUpdateGitFileWithMessage(string projectId, string repoId, string branch, string filePath, string fileContent, string commitMessage, string pat, string orgName, string? connectionId = null);
    Task<string> GetGitFile(string filePath, string projectId, string repoId, string branch, string pat, string orgName, string? connectionId = null);

    // Pipeline Methods
    Task<DataObjects.DevopsPipelineDefinition> GetDevOpsPipeline(string projectId, int pipelineId, string pat, string orgName, string? connectionId = null);
    Task<List<DataObjects.DevopsPipelineDefinition>> GetDevOpsPipelines(string projectId, string pat, string orgName, string? connectionId = null);
    Task<List<DataObjects.DevOpsBuild>> GetPipelineRuns(int pipelineId, string projectId, string pat, string orgName, int skip = 0, int top = 10, string? connectionId = null);
    Task<string> GenerateYmlFileContents(string devopsProjectId, string devopsRepoId, string devopsBranch, int? devopsPipelineId, string? devopsPipelineName, string codeProjectId, string codeRepoId, string codeBranchName, string codeCsProjectFile, Dictionary<GlobalSettings.EnvironmentType, DataObjects.EnvSetting> environmentSettings, string pat, string orgName, string? connectionId = null);
    Task<string> GeneratePipelineVariableReplacementText(string projectName, string csProjectFile, Dictionary<GlobalSettings.EnvironmentType, DataObjects.EnvSetting> environmentSettings);
    Task<string> GeneratePipelineDeployStagesReplacementText(Dictionary<GlobalSettings.EnvironmentType, DataObjects.EnvSetting> environmentSettings);
    Task<DataObjects.BuildDefinition> CreateOrUpdateDevopsPipeline(string devopsProjectId, string devopsRepoId, string devopsBranchName, int? devopsPipelineId, string? devopsPipelineName, string? devopsPipelineYmlFileName, string codeProjectId, string codeRepoId, string codeBranchName, string codeCsProjectFile, Dictionary<GlobalSettings.EnvironmentType, DataObjects.EnvSetting> environmentSettings, string pat, string orgName, string? connectionId = null);
    
    // Pipeline Dashboard Methods
    Task<DataObjects.PipelineDashboardResponse> GetPipelineDashboardAsync(string pat, string orgName, string projectId, string? connectionId = null);
    Task<DataObjects.PipelineRunsResponse> GetPipelineRunsForDashboardAsync(string pat, string orgName, string projectId, int pipelineId, int top = 5, string? connectionId = null);
    Task<DataObjects.PipelineYamlResponse> GetPipelineYamlContentAsync(string pat, string orgName, string projectId, int pipelineId, string? connectionId = null);
    DataObjects.ParsedPipelineSettings ParsePipelineYaml(string yamlContent, int? pipelineId = null, string? pipelineName = null, string? pipelinePath = null);
    Task<Dictionary<string, DataObjects.IISInfo?>> GetDevOpsIISInfoAsync();
    Task<DataObjects.BooleanResponse> RunPipelineAsync(string pat, string orgName, string projectId, int pipelineId);
    Task<DataObjects.BuildTimelineResponse> GetBuildTimelineAsync(string pat, string orgName, string projectId, int buildId);
    Task<DataObjects.BuildLogResponse> GetBuildJobLogsAsync(string pat, string orgName, string projectId, int buildId, string jobId);
    Task<DataObjects.OrgHealthResponse> GetOrgHealthAsync(string pat, string orgName, string projectId, int buildsPerPipeline = 10);

    // Public Git Repository Import Methods
    Task<DataObjects.PublicGitRepoInfo> ValidatePublicGitRepoAsync(string url);
    Task<DataObjects.ImportConflictInfo> CheckImportConflictsAsync(string pat, string orgName, string? targetProjectId, string? newProjectName, string repoName, string sourceUrl);
    Task<DataObjects.DevopsProjectInfo> CreateDevOpsProjectAsync(string pat, string orgName, string projectName, string? description = null, string? connectionId = null);
    Task<DataObjects.DevopsGitRepoInfo> CreateDevOpsRepoAsync(string pat, string orgName, string projectId, string repoName, string? connectionId = null);
    Task<DataObjects.ImportPublicRepoResponse> ImportPublicRepoAsync(string pat, string orgName, DataObjects.ImportPublicRepoRequest request, string? connectionId = null);
    Task<DataObjects.ImportPublicRepoResponse> GetImportStatusAsync(string pat, string orgName, string projectId, string repoId, int importRequestId, string? connectionId = null);
}

public partial class DataAccess
{
    private IMemoryCache? _cache;

    private VssConnection CreateConnection(string pat, string orgName)
    {
        var collectionUri = new Uri($"https://dev.azure.com/{orgName}");
        var credentials = new VssBasicCredential(string.Empty, pat);
        return new VssConnection(collectionUri, credentials);
    }
}

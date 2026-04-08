using FreeCICD.Server.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreeCICD.Server.Controllers;

// FreeCICD-specific API endpoints for Azure DevOps integration

public partial class DataController
{
    /// <summary>
    /// Gets DevOps config from appsettings (for logged-in users)
    /// </summary>
    private (string orgName, string pat, string projectId, string repoId, string branch) GetReleasePipelinesDevOpsConfig()
    {
        string orgName = configurationHelper?.OrgName ?? "";
        string pat = configurationHelper?.PAT ?? "";
        string projectId = configurationHelper?.ProjectId ?? "";
        string repoId = configurationHelper?.RepoId ?? "";
        string branch = configurationHelper?.Branch ?? "";
        return (orgName, pat, projectId, repoId, branch);
    }

    #region Pipeline Dashboard Endpoints

    /// <summary>
    /// Joins the caller's SignalR connection to the live pipeline monitor group.
    /// The background PipelineMonitorService broadcasts status changes to this group.
    /// </summary>
    [HttpPost("~/api/Pipelines/monitor/join")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.BooleanResponse>> JoinPipelineMonitor([FromQuery] string connectionId)
    {
        var output = new DataObjects.BooleanResponse();

        if (string.IsNullOrWhiteSpace(connectionId)) {
            output.Messages.Add("ConnectionId is required");
            return Ok(output);
        }

        if (_signalR != null) {
            await _signalR.Groups.AddToGroupAsync(connectionId, DataObjects.SignalRUpdateType.PipelineMonitorGroup);

            // Update connection info tracking
            var connInfo = freecicdHub.GetConnectionInfo(connectionId);
            if (connInfo != null && !connInfo.Groups.Contains(DataObjects.SignalRUpdateType.PipelineMonitorGroup)) {
                connInfo.Groups.Add(DataObjects.SignalRUpdateType.PipelineMonitorGroup);
            }

            output.Result = true;
        }

        return Ok(output);
    }

    /// <summary>
    /// Removes the caller's SignalR connection from the live pipeline monitor group.
    /// </summary>
    [HttpPost("~/api/Pipelines/monitor/leave")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.BooleanResponse>> LeavePipelineMonitor([FromQuery] string connectionId)
    {
        var output = new DataObjects.BooleanResponse();

        if (string.IsNullOrWhiteSpace(connectionId)) {
            output.Messages.Add("ConnectionId is required");
            return Ok(output);
        }

        if (_signalR != null) {
            await _signalR.Groups.RemoveFromGroupAsync(connectionId, DataObjects.SignalRUpdateType.PipelineMonitorGroup);

            // Update connection info tracking
            var connInfo = freecicdHub.GetConnectionInfo(connectionId);
            if (connInfo != null) {
                connInfo.Groups.Remove(DataObjects.SignalRUpdateType.PipelineMonitorGroup);
            }

            output.Result = true;
        }

        return Ok(output);
    }

    /// <summary>
    /// Queues a new run for a specific pipeline (equivalent to clicking "Run pipeline" in Azure DevOps).
    /// </summary>
    [HttpPost("~/api/Pipelines/{pipelineId}/run")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.BooleanResponse>> RunPipeline(int pipelineId, [FromQuery] string? projectId = null, [FromQuery] string? pat = null, [FromQuery] string? orgName = null)
    {
        var output = new DataObjects.BooleanResponse();

        try {
            if (CurrentUser.Enabled) {
                var config = GetReleasePipelinesDevOpsConfig();
                output = await da.RunPipelineAsync(config.pat, config.orgName, projectId ?? config.projectId, pipelineId);
            } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrWhiteSpace(projectId)) {
                output = await da.RunPipelineAsync(pat, orgName, projectId, pipelineId);
            } else {
                return BadRequest("No PAT or OrgName provided and user is not logged in.");
            }
        } catch (Exception ex) {
            output.Messages.Add($"Error running pipeline: {ex.Message}");
        }

        return Ok(output);
    }
    /// <summary>
    /// Gets the detailed build timeline (stages + jobs) for a specific build.
    /// Used by the expandable row detail view on the dashboard.
    /// </summary>
    [HttpGet("~/api/Pipelines/builds/{buildId}/timeline")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.BuildTimelineResponse>> GetBuildTimeline(int buildId, [FromQuery] string? projectId = null, [FromQuery] string? pat = null, [FromQuery] string? orgName = null)
    {
        DataObjects.BuildTimelineResponse output;

        try {
            if (CurrentUser.Enabled) {
                var config = GetReleasePipelinesDevOpsConfig();
                output = await da.GetBuildTimelineAsync(config.pat, config.orgName, projectId ?? config.projectId, buildId);
            } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrWhiteSpace(projectId)) {
                output = await da.GetBuildTimelineAsync(pat, orgName, projectId, buildId);
            } else {
                return BadRequest("No PAT or OrgName provided and user is not logged in.");
            }
        } catch (Exception ex) {
            output = new DataObjects.BuildTimelineResponse { Success = false, ErrorMessage = ex.Message };
        }

        return Ok(output);
    }
    /// <summary>
    /// Gets log content for a specific build job.
    /// </summary>
    [HttpGet("~/api/Pipelines/builds/{buildId}/jobs/{jobId}/logs")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.BuildLogResponse>> GetBuildJobLogs(int buildId, string jobId, [FromQuery] string? projectId = null, [FromQuery] string? pat = null, [FromQuery] string? orgName = null)
    {
        DataObjects.BuildLogResponse output;

        try {
            if (CurrentUser.Enabled) {
                var config = GetReleasePipelinesDevOpsConfig();
                output = await da.GetBuildJobLogsAsync(config.pat, config.orgName, projectId ?? config.projectId, buildId, jobId);
            } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrWhiteSpace(projectId)) {
                output = await da.GetBuildJobLogsAsync(pat, orgName, projectId, buildId, jobId);
            } else {
                return BadRequest("No PAT or OrgName provided and user is not logged in.");
            }
        } catch (Exception ex) {
            output = new DataObjects.BuildLogResponse { Success = false, ErrorMessage = ex.Message };
        }

        return Ok(output);
    }

    /// <summary>
    /// Gets organization-wide pipeline health trends (recent build results for all pipelines).
    /// </summary>
    [HttpGet("~/api/Pipelines/health")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.OrgHealthResponse>> GetOrgHealth([FromQuery] int top = 10, [FromQuery] string? projectId = null, [FromQuery] string? pat = null, [FromQuery] string? orgName = null)
    {
        DataObjects.OrgHealthResponse output;

        try {
            if (CurrentUser.Enabled) {
                var config = GetReleasePipelinesDevOpsConfig();
                output = await da.GetOrgHealthAsync(config.pat, config.orgName, projectId ?? config.projectId, top);
            } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrWhiteSpace(projectId)) {
                output = await da.GetOrgHealthAsync(pat, orgName, projectId, top);
            } else {
                return BadRequest("No PAT or OrgName provided and user is not logged in.");
            }
        } catch (Exception ex) {
            output = new DataObjects.OrgHealthResponse { Success = false, ErrorMessage = ex.Message };
        }

        return Ok(output);
    }
    /// <summary>
    /// Gets all pipelines for the dashboard view with status information.
    /// </summary>
    [HttpGet($"~/{DataObjects.Endpoints.PipelineDashboard.GetPipelinesList}")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.PipelineDashboardResponse>> GetPipelinesDashboard([FromQuery] string? projectId = null, [FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        DataObjects.PipelineDashboardResponse output;

        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GetPipelineDashboardAsync(config.pat, config.orgName, projectId ?? config.projectId, connectionId);
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrWhiteSpace(projectId)) {
            output = await da.GetPipelineDashboardAsync(pat, orgName, projectId, connectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    /// <summary>
    /// Gets recent runs for a specific pipeline.
    /// </summary>
    [HttpGet("~/api/Pipelines/{pipelineId}/runs")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.PipelineRunsResponse>> GetPipelineRuns(int pipelineId, [FromQuery] int top = 5, [FromQuery] string? projectId = null, [FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        DataObjects.PipelineRunsResponse output;

        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GetPipelineRunsForDashboardAsync(config.pat, config.orgName, projectId ?? config.projectId, pipelineId, top, connectionId);
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrWhiteSpace(projectId)) {
            output = await da.GetPipelineRunsForDashboardAsync(pat, orgName, projectId, pipelineId, top, connectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    /// <summary>
    /// Gets the raw YAML content for a specific pipeline.
    /// </summary>
    [HttpGet("~/api/Pipelines/{pipelineId}/yaml")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.PipelineYamlResponse>> GetPipelineYaml(int pipelineId, [FromQuery] string? projectId = null, [FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        DataObjects.PipelineYamlResponse output;

        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GetPipelineYamlContentAsync(config.pat, config.orgName, projectId ?? config.projectId, pipelineId, connectionId);
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrWhiteSpace(projectId)) {
            output = await da.GetPipelineYamlContentAsync(pat, orgName, projectId, pipelineId, connectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    /// <summary>
    /// Parses a pipeline's YAML and returns extracted settings for import.
    /// </summary>
    [HttpPost("~/api/Pipelines/{pipelineId}/parse")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.ParsedPipelineSettings>> ParsePipelineYaml(int pipelineId, [FromQuery] string? projectId = null, [FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        // First get the YAML content
        DataObjects.PipelineYamlResponse yamlResponse;
        string pipelineName = string.Empty;
        string pipelinePath = string.Empty;

        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            yamlResponse = await da.GetPipelineYamlContentAsync(config.pat, config.orgName, projectId ?? config.projectId, pipelineId, connectionId);
            
            // Get pipeline name and path
            try {
                var pipeline = await da.GetDevOpsPipeline(projectId ?? config.projectId, pipelineId, config.pat, config.orgName, connectionId);
                pipelineName = pipeline.Name;
                pipelinePath = pipeline.Path;
            } catch { }
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrWhiteSpace(projectId)) {
            yamlResponse = await da.GetPipelineYamlContentAsync(pat, orgName, projectId, pipelineId, connectionId);
            
            // Get pipeline name and path
            try {
                var pipeline = await da.GetDevOpsPipeline(projectId, pipelineId, pat, orgName, connectionId);
                pipelineName = pipeline.Name;
                pipelinePath = pipeline.Path;
            } catch { }
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        if (!yamlResponse.Success) {
            return BadRequest(yamlResponse.ErrorMessage ?? "Failed to fetch pipeline YAML.");
        }

        // Parse the YAML
        var parsedSettings = da.ParsePipelineYaml(yamlResponse.Yaml, pipelineId, pipelineName, pipelinePath);
        return Ok(parsedSettings);
    }

    #endregion Pipeline Dashboard Endpoints

    #region Git & Pipeline Endpoints

    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetDevOpsBranches}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DataObjects.DevopsGitRepoBranchInfo>>> GetDevOpsBranches([FromQuery] string projectId, [FromQuery] string repoId, [FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        List<DataObjects.DevopsGitRepoBranchInfo> output;
        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GetDevOpsBranchesAsync(config.pat, config.orgName, projectId, repoId, connectionId);
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName)) {
            output = await da.GetDevOpsBranchesAsync(pat, orgName, projectId, repoId, connectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetDevOpsFiles}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DataObjects.DevopsFileItem>>> GetDevOpsFiles([FromQuery] string projectId, [FromQuery] string repoId, [FromQuery] string branchName, [FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        List<DataObjects.DevopsFileItem> output;
        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GetDevOpsFilesAsync(config.pat, config.orgName, projectId, repoId, branchName, connectionId);
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName)) {
            output = await da.GetDevOpsFilesAsync(pat, orgName, projectId, repoId, branchName, connectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetDevOpsProjects}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DataObjects.DevopsProjectInfo>>> GetDevOpsProjects([FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        List<DataObjects.DevopsProjectInfo> output;

        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GetDevOpsProjectsAsync(config.pat, config.orgName, connectionId);
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName)) {
            output = await da.GetDevOpsProjectsAsync(pat, orgName, connectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetDevOpsRepos}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DataObjects.DevopsGitRepoInfo>>> GetDevOpsRepos([FromQuery] string projectId, [FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        List<DataObjects.DevopsGitRepoInfo> output;

        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GetDevOpsReposAsync(config.pat, config.orgName, projectId, connectionId);
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName)) {
            output = await da.GetDevOpsReposAsync(pat, orgName, projectId, connectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetDevOpsPipelines}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DataObjects.DevopsPipelineDefinition>>> GetDevOpsPipelines([FromQuery] string? projectId, [FromQuery] string? repoId, [FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        List<DataObjects.DevopsPipelineDefinition> output;

        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GetDevOpsPipelines(config.projectId, config.pat, config.orgName, connectionId);
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrEmpty(projectId)) {
            output = await da.GetDevOpsPipelines(projectId, pat, orgName, connectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetDevOpsYmlFileContent}")]
    [AllowAnonymous]
    public async Task<ActionResult<string>> GetDevOpsYmlFileContent(string? filePath, [FromQuery] string? projectId, [FromQuery] string repoId, [FromQuery] string branchName, [FromQuery] string? pat = null, [FromQuery] string? orgName = null, [FromQuery] string? connectionId = null)
    {
        string output = string.Empty;

        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GetGitFile(filePath ?? "", config.projectId, config.repoId, config.branch, config.pat, config.orgName, connectionId);
        } else if (!string.IsNullOrWhiteSpace(pat) && !string.IsNullOrWhiteSpace(orgName) && !string.IsNullOrEmpty(projectId)) {
            output = await da.GetGitFile(filePath ?? "", projectId, repoId, branchName, pat, orgName, connectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    [HttpGet($"~/{DataObjects.Endpoints.DevOps.GetDevOpsIISInfo}")]
    [AllowAnonymous]
    public async Task<ActionResult<Dictionary<string, DataObjects.IISInfo?>>> GetDevOpsIISInfo()
    {
        Dictionary<string, DataObjects.IISInfo?> output = new();

        if (CurrentUser.Enabled) {
            output = await da.GetDevOpsIISInfoAsync();
        }

        return Ok(output);
    }

    /// <summary>
    /// Shows a preview of the contents of the yml file we are generating for a given DevopsPipelineRequest.
    /// </summary>
    [HttpPost($"{DataObjects.Endpoints.DevOps.PreviewDevOpsYmlFileContents}")]
    [AllowAnonymous]
    public async Task<ActionResult<string>> PreviewDevOpsYmlFileContents([FromBody] DataObjects.DevOpsPipelineRequest request)
    {
        string output = string.Empty;

        if (request == null) {
            return BadRequest("Request body cannot be null.");
        }

        if (CurrentUser.Enabled) {
            var config = GetReleasePipelinesDevOpsConfig();
            output = await da.GenerateYmlFileContents(config.projectId, config.repoId, config.branch, request.PipelineId, request.PipelineName, request.ProjectId, request.RepoId, request.Branch, request.CsProjectFile, request.EnvironmentSettings, config.pat, config.orgName, request.ConnectionId);
        } else if (!string.IsNullOrWhiteSpace(request.Pat) && !string.IsNullOrWhiteSpace(request.OrgName)) {
            output = await da.GenerateYmlFileContents(request.ProjectId, request.RepoId, request.Branch, request.PipelineId, request.PipelineName, request.ProjectId, request.RepoId, request.Branch, request.CsProjectFile, request.EnvironmentSettings, request.Pat, request.OrgName, request.ConnectionId);
        } else {
            return BadRequest("No PAT or OrgName provided and user is not logged in.");
        }

        return Ok(output);
    }

    /// <summary>
    /// Create or update an Azure DevOps pipeline and its YAML file + variable groups in one call.
    /// </summary>
    [HttpPost($"{DataObjects.Endpoints.DevOps.CreateOrUpdateDevOpsPipeline}")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.BuildDefinition>> CreateOrUpdateDevOpsPipeline([FromBody] DataObjects.DevOpsPipelineRequest request)
    {
        DataObjects.BuildDefinition output = new DataObjects.BuildDefinition();
        if (request == null) {
            return BadRequest("Request body cannot be null.");
        }

        try {
            if (CurrentUser.Enabled) {
                var config = GetReleasePipelinesDevOpsConfig();
                output = await da.CreateOrUpdateDevopsPipeline(config.projectId, config.repoId, config.branch, request.PipelineId, request.PipelineName, request.YAMLFileName, request.ProjectId, request.RepoId, request.Branch, request.CsProjectFile, request.EnvironmentSettings ?? new(), config.pat, config.orgName, request.ConnectionId);
            } else if (!string.IsNullOrWhiteSpace(request.Pat) && !string.IsNullOrWhiteSpace(request.OrgName)) {
                output = await da.CreateOrUpdateDevopsPipeline(request.ProjectId, request.RepoId, request.Branch, request.PipelineId, request.PipelineName, request.YAMLFileName, request.ProjectId, request.RepoId, request.Branch, request.CsProjectFile, request.EnvironmentSettings, request.Pat, request.OrgName, request.ConnectionId);
            } else {
                return BadRequest("No PAT or OrgName provided and user is not logged in.");
            }
        } catch (System.Exception ex) {
            return BadRequest($"Error creating/updating pipeline: {ex.Message}");
        }

        return Ok(output);
    }

    #endregion Git & Pipeline Endpoints

    #region Public Git Repository Import Endpoints

    /// <summary>
    /// Validates a public Git repository URL and retrieves metadata.
    /// No authentication required - just validates the URL exists and extracts info.
    /// </summary>
    [HttpPost($"~/{DataObjects.Endpoints.Import.ValidateUrl}")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.PublicGitRepoInfo>> ValidatePublicRepoUrl([FromBody] ValidateUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Url)) {
            return BadRequest("URL is required.");
        }

        var result = await da.ValidatePublicGitRepoAsync(request.Url);
        return Ok(result);
    }

    /// <summary>
    /// Checks for conflicts before starting an import (project/repo name conflicts, duplicate imports).
    /// Requires PAT via headers to check against Azure DevOps.
    /// </summary>
    [HttpPost($"~/{DataObjects.Endpoints.Import.CheckConflicts}")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.ImportConflictInfo>> CheckImportConflicts([FromBody] CheckConflictsRequest request)
    {
        // Get PAT and org from headers (following existing pattern)
        var pat = Request.Headers["DevOpsPAT"].FirstOrDefault();
        var orgName = Request.Headers["DevOpsOrg"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(pat) || string.IsNullOrWhiteSpace(orgName)) {
            // Try from logged in user config
            if (CurrentUser.Enabled) {
                var config = GetReleasePipelinesDevOpsConfig();
                pat = config.pat;
                orgName = config.orgName;
            } else {
                return BadRequest("PAT and OrgName are required. Pass via DevOpsPAT and DevOpsOrg headers.");
            }
        }

        if (string.IsNullOrWhiteSpace(request?.RepoName)) {
            return BadRequest("RepoName is required.");
        }

        var result = await da.CheckImportConflictsAsync(
            pat, orgName,
            request.TargetProjectId,
            request.NewProjectName,
            request.RepoName,
            request.SourceUrl ?? ""
        );

        return Ok(result);
    }

    /// <summary>
    /// Starts importing a public Git repository into Azure DevOps.
    /// Creates project (if needed), creates repo, and initiates the import.
    /// </summary>
    [HttpPost($"~/{DataObjects.Endpoints.Import.Start}")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.ImportPublicRepoResponse>> StartPublicRepoImport([FromBody] DataObjects.ImportPublicRepoRequest request, [FromQuery] string? connectionId = null)
    {
        // Get PAT and org from headers
        var pat = Request.Headers["DevOpsPAT"].FirstOrDefault();
        var orgName = Request.Headers["DevOpsOrg"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(pat) || string.IsNullOrWhiteSpace(orgName)) {
            if (CurrentUser.Enabled) {
                var config = GetReleasePipelinesDevOpsConfig();
                pat = config.pat;
                orgName = config.orgName;
            } else {
                return BadRequest("PAT and OrgName are required. Pass via DevOpsPAT and DevOpsOrg headers.");
            }
        }

        if (request == null || string.IsNullOrWhiteSpace(request.NewProjectName)) {
            return BadRequest("NewProjectName is required.");
        }

        // For URL-based imports, SourceUrl is required
        if (request.Method != DataObjects.ImportMethod.ZipUpload && string.IsNullOrWhiteSpace(request.SourceUrl)) {
            return BadRequest("SourceUrl is required for URL-based imports.");
        }

        var result = await da.ImportPublicRepoAsync(pat, orgName, request, connectionId);
        return Ok(result);
    }

    /// <summary>
    /// Gets the status of an import operation.
    /// Poll this endpoint to track import progress.
    /// </summary>
    [HttpGet($"~/{DataObjects.Endpoints.Import.GetStatus}/{{projectId}}/{{repoId}}/{{requestId}}")]
    [AllowAnonymous]
    public async Task<ActionResult<DataObjects.ImportPublicRepoResponse>> GetPublicRepoImportStatus(string projectId, string repoId, int requestId, [FromQuery] string? connectionId = null)
    {
        // Get PAT and org from headers
        var pat = Request.Headers["DevOpsPAT"].FirstOrDefault();
        var orgName = Request.Headers["DevOpsOrg"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(pat) || string.IsNullOrWhiteSpace(orgName)) {
            if (CurrentUser.Enabled) {
                var config = GetReleasePipelinesDevOpsConfig();
                pat = config.pat;
                orgName = config.orgName;
            } else {
                return BadRequest("PAT and OrgName are required. Pass via DevOpsPAT and DevOpsOrg headers.");
            }
        }

        var result = await da.GetImportStatusAsync(pat, orgName, projectId, repoId, requestId, connectionId);
        return Ok(result);
    }

    /// <summary>
    /// Uploads a ZIP file for import. The file is stored temporarily and can be
    /// referenced by FileId when calling StartPublicRepoImport with Method=ZipUpload.
    /// Max size: 100MB. Files are automatically cleaned up after 1 hour.
    /// </summary>
    [HttpPost($"~/{DataObjects.Endpoints.Import.UploadZip}")]
    [AllowAnonymous]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB limit
    public async Task<ActionResult<DataObjects.UploadZipResponse>> UploadImportZip(IFormFile file)
    {
        var result = new DataObjects.UploadZipResponse();

        try {
            if (file == null || file.Length == 0) {
                result.Success = false;
                result.ErrorMessage = "No file uploaded.";
                return BadRequest(result);
            }

            // Validate file extension
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (extension != ".zip") {
                result.Success = false;
                result.ErrorMessage = "Only .zip files are allowed.";
                return BadRequest(result);
            }

            // Validate file size (100MB max)
            if (file.Length > 100 * 1024 * 1024) {
                result.Success = false;
                result.ErrorMessage = "File size exceeds 100MB limit.";
                return BadRequest(result);
            }

            // Generate unique file ID
            var fileId = Guid.NewGuid();
            
            // Create temp directory for uploads if it doesn't exist
            var uploadDir = Path.Combine(Path.GetTempPath(), "FreeCICD_Imports");
            Directory.CreateDirectory(uploadDir);
            
            // Save file with unique ID
            var filePath = Path.Combine(uploadDir, $"{fileId}.zip");
            using (var stream = new FileStream(filePath, FileMode.Create)) {
                await file.CopyToAsync(stream);
            }

            // Try to detect repo name and file count from ZIP
            int fileCount = 0;
            string? detectedName = null;
            try {
                using var zipArchive = System.IO.Compression.ZipFile.OpenRead(filePath);
                fileCount = zipArchive.Entries.Count(e => !string.IsNullOrEmpty(e.Name));
                
                // GitHub ZIPs have format: reponame-branchname/...
                // Try to detect repo name from first directory
                var firstEntry = zipArchive.Entries.FirstOrDefault(e => e.FullName.Contains('/'));
                if (firstEntry != null) {
                    var topDir = firstEntry.FullName.Split('/')[0];
                    // Remove branch suffix (e.g., "aspnetcore-main" -> "aspnetcore")
                    if (topDir.Contains('-')) {
                        detectedName = topDir.Substring(0, topDir.LastIndexOf('-'));
                    } else {
                        detectedName = topDir;
                    }
                }
            } catch {
                // Ignore ZIP inspection errors
            }

            result.Success = true;
            result.FileId = fileId;
            result.FileName = file.FileName;
            result.FileSizeBytes = file.Length;
            result.FileCount = fileCount;
            result.DetectedRepoName = detectedName;

            return Ok(result);
        } catch (Exception ex) {
            result.Success = false;
            result.ErrorMessage = $"Upload failed: {ex.Message}";
            return StatusCode(500, result);
        }
    }

    #endregion Public Git Repository Import Endpoints

    #region Import Request DTOs

    /// <summary>Request DTO for URL validation.</summary>
    public record ValidateUrlRequest(string Url);

    /// <summary>Request DTO for conflict checking.</summary>
    public record CheckConflictsRequest(
        string? TargetProjectId,
        string? NewProjectName,
        string RepoName,
        string? SourceUrl
    );

    #endregion Import Request DTOs

    #region SignalR Admin Endpoints

    /// <summary>
    /// Gets all active SignalR connections across all hubs.
    /// Requires admin access.
    /// </summary>
    [HttpGet("~/api/Admin/SignalRConnections")]
    [Authorize(Policy = Policies.Admin)]
    public ActionResult<DataObjects.SignalRConnectionsResponse> GetSignalRConnections()
    {
        var response = new DataObjects.SignalRConnectionsResponse {
            Success = true
        };

        try {
            // Get connections from freecicdHub
            var connections = Hubs.freecicdHub.GetActiveConnectionsList();
            
            var hubInfo = new DataObjects.SignalRHubInfo {
                HubName = "freecicdHub",
                Connections = connections
            };
            
            response.Hubs.Add(hubInfo);
            response.TotalConnectionCount = connections.Count;
        } catch (Exception ex) {
            response.Success = false;
            response.ErrorMessage = $"Error retrieving connections: {ex.Message}";
        }

        return Ok(response);
    }

    /// <summary>
    /// Sends an alert message to a specific SignalR connection.
    /// Requires admin access.
    /// </summary>
    [HttpPost("~/api/Admin/SendAlert")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<DataObjects.SendAlertResponse>> SendAlertToConnection([FromBody] DataObjects.SendAlertRequest request)
    {
        var response = new DataObjects.SendAlertResponse {
            ConnectionId = request.ConnectionId
        };

        if (string.IsNullOrWhiteSpace(request.ConnectionId)) {
            response.Success = false;
            response.ErrorMessage = "Connection ID is required.";
            return BadRequest(response);
        }

        if (string.IsNullOrWhiteSpace(request.Message)) {
            response.Success = false;
            response.ErrorMessage = "Message is required.";
            return BadRequest(response);
        }

        try {
            // Check if connection exists
            if (!Hubs.freecicdHub.ConnectionExists(request.ConnectionId)) {
                response.Success = false;
                response.ErrorMessage = "Connection not found. The user may have disconnected.";
                return NotFound(response);
            }

            // Create the SignalR update
            var update = new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.AdminAlert,
                Message = request.Message,
                UserId = CurrentUser.UserId,
                UserDisplayName = CurrentUser.DisplayName ?? "Admin",
                ObjectAsString = request.MessageType,
                Object = new {
                    AutoHide = request.AutoHide,
                    MessageType = request.MessageType,
                    SenderName = CurrentUser.DisplayName ?? "Admin"
                }
            };

            // Send to specific connection using injected hub context
            if (_signalR != null) {
                await _signalR.Clients.Client(request.ConnectionId).SignalRUpdate(update);
                response.Success = true;
            } else {
                response.Success = false;
                response.ErrorMessage = "SignalR hub context not available.";
            }
        } catch (Exception ex) {
            response.Success = false;
            response.ErrorMessage = $"Error sending alert: {ex.Message}";
            return StatusCode(500, response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Sends an alert message to all connected clients.
    /// Requires admin access.
    /// </summary>
    [HttpPost("~/api/Admin/BroadcastAlert")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<DataObjects.SendAlertResponse>> BroadcastAlert([FromBody] DataObjects.SendAlertRequest request)
    {
        var response = new DataObjects.SendAlertResponse();

        if (string.IsNullOrWhiteSpace(request.Message)) {
            response.Success = false;
            response.ErrorMessage = "Message is required.";
            return BadRequest(response);
        }

        try {
            // Create the SignalR update
            var update = new DataObjects.SignalRUpdate {
                UpdateType = DataObjects.SignalRUpdateType.AdminAlert,
                Message = request.Message,
                UserId = CurrentUser.UserId,
                UserDisplayName = CurrentUser.DisplayName ?? "Admin",
                ObjectAsString = request.MessageType,
                Object = new {
                    AutoHide = request.AutoHide,
                    MessageType = request.MessageType,
                    SenderName = CurrentUser.DisplayName ?? "Admin"
                }
            };

            // Send to all clients using injected hub context
            if (_signalR != null) {
                await _signalR.Clients.All.SignalRUpdate(update);
                response.Success = true;
            } else {
                response.Success = false;
                response.ErrorMessage = "SignalR hub context not available.";
            }
        } catch (Exception ex) {
            response.Success = false;
            response.ErrorMessage = $"Error broadcasting alert: {ex.Message}";
            return StatusCode(500, response);
        }

        return Ok(response);
    }

    #endregion SignalR Admin Endpoints
}

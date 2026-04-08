using FreeExamples.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreeExamples.Server.Controllers;

/// <summary>
/// API endpoints for browsing a git repository. Uses the GitBrowserService
/// to clone public repos and serve their directory/file contents.
/// </summary>
public partial class DataController
{
    /// <summary>
    /// GetMany for git repo entries: POST a GitBrowseRequest with RepoUrl and optional Path.
    /// Returns list of GitRepoEntry (folders first, then files).
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetGitRepoContents")]
    public async Task<ActionResult<List<DataObjects.GitRepoEntry>>> GetGitRepoContents(
        [FromBody] DataObjects.GitBrowseRequest request,
        [FromServices] GitBrowserService gitService)
    {
        if (string.IsNullOrWhiteSpace(request.RepoUrl))
            return Ok(new List<DataObjects.GitRepoEntry>());

        var entries = await gitService.BrowseAsync(request.RepoUrl, request.Path);
        return Ok(entries);
    }

    /// <summary>
    /// Get a single file's content from a git repo.
    /// POST a GitFileRequest with RepoUrl and FilePath.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetGitFileContent")]
    public async Task<ActionResult<DataObjects.GitFileContent>> GetGitFileContent(
        [FromBody] DataObjects.GitFileRequest request,
        [FromServices] GitBrowserService gitService)
    {
        if (string.IsNullOrWhiteSpace(request.RepoUrl) || string.IsNullOrWhiteSpace(request.FilePath))
            return Ok(new DataObjects.GitFileContent { Content = "No file specified." });

        var content = await gitService.GetFileAsync(request.RepoUrl, request.FilePath);
        return Ok(content);
    }
}

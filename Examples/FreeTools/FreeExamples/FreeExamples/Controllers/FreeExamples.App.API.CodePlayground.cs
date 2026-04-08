using FreeExamples.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FreeExamples.Server.Controllers;

/// <summary>
/// API endpoints for the Code Playground demo.
/// Three-endpoint pattern: GetMany, SaveMany, DeleteMany for CodeSnippet.
/// Plus an "execute" endpoint for the API Notebook tab.
/// </summary>
public partial class DataController
{
    private static readonly JsonSerializerOptions _prettyJson = new() { WriteIndented = true };

    /// <summary>
    /// GetMany: null/empty → all snippets, list of IDs → filtered.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetCodeSnippets")]
    public ActionResult<List<DataObjects.CodeSnippet>> GetCodeSnippets(
        List<Guid>? ids,
        [FromServices] CodeSnippetService snippetService)
    {
        return Ok(snippetService.GetSnippets(ids));
    }

    /// <summary>
    /// Save a single snippet (insert or update).
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/SaveCodeSnippet")]
    public ActionResult<DataObjects.CodeSnippet> SaveCodeSnippet(
        [FromBody] DataObjects.CodeSnippet snippet,
        [FromServices] CodeSnippetService snippetService)
    {
        return Ok(snippetService.SaveSnippet(snippet));
    }

    /// <summary>
    /// Delete a snippet by ID.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/DeleteCodeSnippet")]
    public ActionResult<DataObjects.BooleanResponse> DeleteCodeSnippet(
        [FromBody] Guid id,
        [FromServices] CodeSnippetService snippetService)
    {
        return Ok(new DataObjects.BooleanResponse { Result = snippetService.DeleteSnippet(id) });
    }

    /// <summary>
    /// Execute a request against a selected internal API endpoint.
    /// Used by the API Notebook tab to POST JSON and get the response.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/ExecuteCodePlayground")]
    public ActionResult<DataObjects.CodePlaygroundResponse> ExecuteCodePlayground(
        [FromBody] DataObjects.CodePlaygroundRequest request)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = new DataObjects.CodePlaygroundResponse();

        try {
            object? result = request.Endpoint switch {
                "GetSampleItems" => da.GetSampleItems(
                    string.IsNullOrWhiteSpace(request.Body) ? null :
                    JsonSerializer.Deserialize<List<Guid>>(request.Body)),

                "GetSampleDashboard" => da.GetSampleDashboard(),

                "GetSampleItemsFiltered" => da.GetSampleItemsFiltered(
                    JsonSerializer.Deserialize<DataObjects.FilterSampleItems>(request.Body)
                    ?? new DataObjects.FilterSampleItems()),

                "GetSampleGraphData" => da.GetSampleGraphData(),

                _ => null,
            };

            if (result != null) {
                response.Success = true;
                response.StatusCode = 200;
                response.ResponseBody = JsonSerializer.Serialize(result, _prettyJson);
            } else {
                response.Success = false;
                response.StatusCode = 404;
                response.ResponseBody = "{ \"error\": \"Unknown endpoint: " + request.Endpoint + "\" }";
            }
        } catch (Exception ex) {
            response.Success = false;
            response.StatusCode = 500;
            response.ResponseBody = "{ \"error\": \"" + ex.Message.Replace("\"", "\\\"") + "\" }";
        }

        sw.Stop();
        response.DurationMs = sw.ElapsedMilliseconds;
        return Ok(response);
    }
}

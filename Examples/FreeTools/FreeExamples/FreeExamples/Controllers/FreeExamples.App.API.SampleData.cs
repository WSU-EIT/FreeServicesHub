using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreeExamples.Server.Controllers;

/// <summary>
/// Three-endpoint CRUD pattern for SampleData: GetMany, SaveMany, DeleteMany.
/// Plus a dashboard aggregate endpoint shared across demo pages.
/// </summary>
public partial class DataController
{
    /// <summary>
    /// GetMany: POST null/empty list → all items, or list of IDs → matching items.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetSampleItems")]
    public ActionResult<List<DataObjects.SampleItem>> GetSampleItems(List<Guid>? ids)
    {
        return Ok(da.GetSampleItems(ids));
    }

    /// <summary>
    /// SaveMany: POST a list of items. If SampleItemId exists → update, otherwise → insert.
    /// Caller may provide their own ID or leave it empty for auto-generation.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/SaveSampleItems")]
    public async Task<ActionResult<List<DataObjects.SampleItem>>> SaveSampleItems(List<DataObjects.SampleItem> items)
    {
        var saved = da.SaveSampleItems(items, CurrentUser);

        // Broadcast each saved item via SignalR so other clients update in real time.
        foreach (var item in saved) {
            await da.SignalRUpdate(new DataObjects.SignalRUpdate {
                TenantId = CurrentUser.TenantId,
                ItemId = item.SampleItemId,
                UserId = CurrentUser.UserId,
                UpdateType = DataObjects.SignalRUpdateType.SampleItemSaved,
                Message = "saved",
                Object = item,
            });
        }

        return Ok(saved);
    }

    /// <summary>
    /// DeleteMany: POST a list of IDs to delete. Empty/null → error.
    /// To delete all: GetSampleItems([]) first, then pass IDs.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/DeleteSampleItems")]
    public async Task<ActionResult<DataObjects.BooleanResponse>> DeleteSampleItems(List<Guid>? ids)
    {
        var result = da.DeleteSampleItems(ids);

        // Broadcast each deleted ID via SignalR.
        if (result.Result && ids != null) {
            foreach (var id in ids) {
                await da.SignalRUpdate(new DataObjects.SignalRUpdate {
                    TenantId = CurrentUser.TenantId,
                    ItemId = id,
                    UserId = CurrentUser.UserId,
                    UpdateType = DataObjects.SignalRUpdateType.SampleItemDeleted,
                    Message = "deleted",
                });
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Dashboard aggregate data — status counts, category breakdowns, timeline.
    /// Shared across dashboard, charts, and reporting demo pages.
    /// </summary>
    [HttpGet]
    [Authorize]
    [Route("~/api/Data/GetSampleDashboard")]
    public ActionResult<DataObjects.SampleDashboard> GetSampleDashboard()
    {
        return Ok(da.GetSampleDashboard());
    }

    /// <summary>
    /// Filtered, sorted, paginated list of sample items.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetSampleItemsFiltered")]
    public ActionResult<DataObjects.FilterSampleItems> GetSampleItemsFiltered(DataObjects.FilterSampleItems filter)
    {
        return Ok(da.GetSampleItemsFiltered(filter));
    }

    /// <summary>
    /// Server-side text file generation for download demo.
    /// </summary>
    [HttpGet]
    [Authorize]
    [Route("~/api/Data/GenerateSampleTextFile")]
    public ActionResult<DataObjects.SampleFileResponse> GenerateSampleTextFile()
    {
        return Ok(da.GenerateSampleTextFile());
    }

    /// <summary>
    /// Server-side CSV export for download demo.
    /// </summary>
    [HttpGet]
    [Authorize]
    [Route("~/api/Data/GenerateSampleCsvExport")]
    public ActionResult<DataObjects.SampleFileResponse> GenerateSampleCsvExport()
    {
        return Ok(da.GenerateSampleCsvExport());
    }

    /// <summary>
    /// Network graph nodes and edges from sample data for vis.js demo.
    /// </summary>
    [HttpGet]
    [Authorize]
    [Route("~/api/Data/GetSampleGraphData")]
    public ActionResult<DataObjects.SampleGraphData> GetSampleGraphData()
    {
        return Ok(da.GetSampleGraphData());
    }
}

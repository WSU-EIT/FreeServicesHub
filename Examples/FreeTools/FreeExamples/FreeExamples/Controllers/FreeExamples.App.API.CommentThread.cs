using FreeExamples.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreeExamples.Server.Controllers;

/// <summary>
/// API endpoints for the Comment Thread demo.
/// </summary>
public partial class DataController
{
    /// <summary>
    /// Get comments for a SampleItem.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/GetComments")]
    public ActionResult<List<DataObjects.SampleComment>> GetComments(
        [FromBody] Guid sampleItemId,
        [FromServices] CommentService commentService)
    {
        return Ok(commentService.GetComments(sampleItemId));
    }

    /// <summary>
    /// Save a comment (insert or update).
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/SaveComment")]
    public async Task<ActionResult<DataObjects.SampleComment>> SaveComment(
        [FromBody] DataObjects.SampleComment comment,
        [FromServices] CommentService commentService)
    {
        var saved = commentService.SaveComment(comment);

        await da.SignalRUpdate(new DataObjects.SignalRUpdate {
            TenantId = CurrentUser.TenantId,
            ItemId = saved.SampleItemId,
            UserId = CurrentUser.UserId,
            UpdateType = DataObjects.SignalRUpdateType.CommentSaved,
            Message = "comment",
            Object = saved,
        });

        return Ok(saved);
    }

    /// <summary>
    /// Delete a comment.
    /// </summary>
    [HttpPost]
    [Authorize]
    [Route("~/api/Data/DeleteComment")]
    public async Task<ActionResult<DataObjects.BooleanResponse>> DeleteComment(
        [FromBody] Guid commentId,
        [FromServices] CommentService commentService)
    {
        var result = commentService.DeleteComment(commentId);

        if (result) {
            await da.SignalRUpdate(new DataObjects.SignalRUpdate {
                TenantId = CurrentUser.TenantId,
                ItemId = commentId,
                UserId = CurrentUser.UserId,
                UpdateType = DataObjects.SignalRUpdateType.CommentDeleted,
                Message = "comment deleted",
            });
        }

        return Ok(new DataObjects.BooleanResponse { Result = result });
    }
}

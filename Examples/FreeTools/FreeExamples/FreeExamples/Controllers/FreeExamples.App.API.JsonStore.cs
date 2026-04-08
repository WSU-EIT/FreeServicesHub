using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FreeExamples.Server.Controllers;

/// <summary>
/// Three-endpoint CRUD for all JSON store entity types.
/// Each entity gets GetMany, SaveMany, DeleteMany — thin wrappers over generic CRUD.
/// </summary>
public partial class DataController
{
    // ── Projects ──
    [HttpPost, Authorize, Route("~/api/Data/GetProjects")]
    public ActionResult<List<DataObjects.Project>> GetProjects([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.Project>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveProjects")]
    public ActionResult<List<DataObjects.Project>> SaveProjects([FromBody] List<DataObjects.Project> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteProjects")]
    public ActionResult<DataObjects.BooleanResponse> DeleteProjects([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.Project>(ids));

    // ── Tickets ──
    [HttpPost, Authorize, Route("~/api/Data/GetTickets")]
    public ActionResult<List<DataObjects.Ticket>> GetTickets([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.Ticket>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveTickets")]
    public ActionResult<List<DataObjects.Ticket>> SaveTickets([FromBody] List<DataObjects.Ticket> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteTickets")]
    public ActionResult<DataObjects.BooleanResponse> DeleteTickets([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.Ticket>(ids));

    // ── Sprints ──
    [HttpPost, Authorize, Route("~/api/Data/GetSprints")]
    public ActionResult<List<DataObjects.Sprint>> GetSprints([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.Sprint>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveSprints")]
    public ActionResult<List<DataObjects.Sprint>> SaveSprints([FromBody] List<DataObjects.Sprint> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteSprints")]
    public ActionResult<DataObjects.BooleanResponse> DeleteSprints([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.Sprint>(ids));

    // ── Board Configs ──
    [HttpPost, Authorize, Route("~/api/Data/GetBoardConfigs")]
    public ActionResult<List<DataObjects.BoardConfig>> GetBoardConfigs([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.BoardConfig>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveBoardConfigs")]
    public ActionResult<List<DataObjects.BoardConfig>> SaveBoardConfigs([FromBody] List<DataObjects.BoardConfig> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteBoardConfigs")]
    public ActionResult<DataObjects.BooleanResponse> DeleteBoardConfigs([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.BoardConfig>(ids));

    // ── Saved Views ──
    [HttpPost, Authorize, Route("~/api/Data/GetSavedViews")]
    public ActionResult<List<DataObjects.SavedView>> GetSavedViews([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.SavedView>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveSavedViews")]
    public ActionResult<List<DataObjects.SavedView>> SaveSavedViews([FromBody] List<DataObjects.SavedView> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteSavedViews")]
    public ActionResult<DataObjects.BooleanResponse> DeleteSavedViews([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.SavedView>(ids));

    // ── Work Orders ──
    [HttpPost, Authorize, Route("~/api/Data/GetWorkOrders")]
    public ActionResult<List<DataObjects.WorkOrder>> GetWorkOrders([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.WorkOrder>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveWorkOrders")]
    public ActionResult<List<DataObjects.WorkOrder>> SaveWorkOrders([FromBody] List<DataObjects.WorkOrder> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteWorkOrders")]
    public ActionResult<DataObjects.BooleanResponse> DeleteWorkOrders([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.WorkOrder>(ids));

    // ── Budget Requests ──
    [HttpPost, Authorize, Route("~/api/Data/GetBudgetRequests")]
    public ActionResult<List<DataObjects.BudgetRequest>> GetBudgetRequests([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.BudgetRequest>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveBudgetRequests")]
    public ActionResult<List<DataObjects.BudgetRequest>> SaveBudgetRequests([FromBody] List<DataObjects.BudgetRequest> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteBudgetRequests")]
    public ActionResult<DataObjects.BooleanResponse> DeleteBudgetRequests([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.BudgetRequest>(ids));

    // ── Equipment ──
    [HttpPost, Authorize, Route("~/api/Data/GetEquipment")]
    public ActionResult<List<DataObjects.Equipment>> GetEquipment([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.Equipment>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveEquipment")]
    public ActionResult<List<DataObjects.Equipment>> SaveEquipment([FromBody] List<DataObjects.Equipment> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteEquipment")]
    public ActionResult<DataObjects.BooleanResponse> DeleteEquipment([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.Equipment>(ids));

    // ── Evaluations ──
    [HttpPost, Authorize, Route("~/api/Data/GetEvaluations")]
    public ActionResult<List<DataObjects.Evaluation>> GetEvaluations([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.Evaluation>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveEvaluations")]
    public ActionResult<List<DataObjects.Evaluation>> SaveEvaluations([FromBody] List<DataObjects.Evaluation> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteEvaluations")]
    public ActionResult<DataObjects.BooleanResponse> DeleteEvaluations([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.Evaluation>(ids));

    // ── Onboarding ──
    [HttpPost, Authorize, Route("~/api/Data/GetOnboarding")]
    public ActionResult<List<DataObjects.Onboarding>> GetOnboarding([FromBody] List<Guid>? ids) => Ok(da.GetJsonRecords<DataObjects.Onboarding>(ids));

    [HttpPost, Authorize, Route("~/api/Data/SaveOnboarding")]
    public ActionResult<List<DataObjects.Onboarding>> SaveOnboarding([FromBody] List<DataObjects.Onboarding> items) => Ok(da.SaveJsonRecords(items, CurrentUser));

    [HttpPost, Authorize, Route("~/api/Data/DeleteOnboarding")]
    public ActionResult<DataObjects.BooleanResponse> DeleteOnboarding([FromBody] List<Guid>? ids) => Ok(da.DeleteJsonRecords<DataObjects.Onboarding>(ids));
}

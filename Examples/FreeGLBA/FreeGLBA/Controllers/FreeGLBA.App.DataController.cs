using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FreeGLBA.Controllers;

namespace FreeGLBA.Server.Controllers;

// ============================================================================
// FREEGLBA PROJECT API ENDPOINTS
// ============================================================================

public partial class DataController
{
    // SourceSystem API Endpoints
    #region SourceSystem

    [HttpPost("api/Data/GetSourceSystems")]
    public async Task<ActionResult<DataObjects.SourceSystemFilterResult>> GetSourceSystems([FromBody] DataObjects.SourceSystemFilter filter)
    {
        return Ok(await da.GetSourceSystemsAsync(filter));
    }

    [HttpPost("api/Data/GetSourceSystem")]
    public async Task<ActionResult<DataObjects.SourceSystem?>> GetSourceSystem([FromBody] Guid id)
    {
        var item = await da.GetSourceSystemAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpGet("api/Data/GetSourceSystemLookups")]
    public async Task<ActionResult<List<DataObjects.SourceSystemLookup>>> GetSourceSystemLookups()
    {
        return Ok(await da.GetSourceSystemLookupsAsync());
    }

    [HttpPost("api/Data/SaveSourceSystem")]
    public async Task<ActionResult<DataObjects.SourceSystem?>> SaveSourceSystem([FromBody] DataObjects.SourceSystem item)
    {
        var result = await da.SaveSourceSystemAsync(item);
        if (result == null) return BadRequest();
        return Ok(result);
    }

    [HttpPost("api/Data/DeleteSourceSystem")]
    public async Task<ActionResult<bool>> DeleteSourceSystem([FromBody] Guid id)
    {
        return Ok(await da.DeleteSourceSystemAsync(id));
    }

    #endregion


    // AccessEvent API Endpoints
    #region AccessEvent

    [HttpPost("api/Data/GetAccessEvents")]
    public async Task<ActionResult<DataObjects.AccessEventFilterResult>> GetAccessEvents([FromBody] DataObjects.AccessEventFilter filter)
    {
        return Ok(await da.GetAccessEventsAsync(filter));
    }

    [HttpPost("api/Data/GetAccessEvent")]
    public async Task<ActionResult<DataObjects.AccessEvent?>> GetAccessEvent([FromBody] Guid id)
    {
        var item = await da.GetAccessEventAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpGet("api/Data/GetAccessEventLookups")]
    public async Task<ActionResult<List<DataObjects.AccessEventLookup>>> GetAccessEventLookups()
    {
        return Ok(await da.GetAccessEventLookupsAsync());
    }

    [HttpPost("api/Data/SaveAccessEvent")]
    public async Task<ActionResult<DataObjects.AccessEvent?>> SaveAccessEvent([FromBody] DataObjects.AccessEvent item)
    {
        var result = await da.SaveAccessEventAsync(item);
        if (result == null) return BadRequest();
        return Ok(result);
    }

    [HttpPost("api/Data/DeleteAccessEvent")]
    public async Task<ActionResult<bool>> DeleteAccessEvent([FromBody] Guid id)
    {
        return Ok(await da.DeleteAccessEventAsync(id));
    }

    #endregion


    // DataSubject API Endpoints
    #region DataSubject

    [HttpPost("api/Data/GetDataSubjects")]
    public async Task<ActionResult<DataObjects.DataSubjectFilterResult>> GetDataSubjects([FromBody] DataObjects.DataSubjectFilter filter)
    {
        return Ok(await da.GetDataSubjectsAsync(filter));
    }

    [HttpPost("api/Data/GetDataSubject")]
    public async Task<ActionResult<DataObjects.DataSubject?>> GetDataSubject([FromBody] Guid id)
    {
        var item = await da.GetDataSubjectAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpGet("api/Data/GetDataSubjectLookups")]
    public async Task<ActionResult<List<DataObjects.DataSubjectLookup>>> GetDataSubjectLookups()
    {
        return Ok(await da.GetDataSubjectLookupsAsync());
    }

    [HttpPost("api/Data/SaveDataSubject")]
    public async Task<ActionResult<DataObjects.DataSubject?>> SaveDataSubject([FromBody] DataObjects.DataSubject item)
    {
        var result = await da.SaveDataSubjectAsync(item);
        if (result == null) return BadRequest();
        return Ok(result);
    }

    [HttpPost("api/Data/DeleteDataSubject")]
    public async Task<ActionResult<bool>> DeleteDataSubject([FromBody] Guid id)
    {
        return Ok(await da.DeleteDataSubjectAsync(id));
    }

    #endregion


    // ComplianceReport API Endpoints
    #region ComplianceReport

    [HttpPost("api/Data/GetComplianceReports")]
    public async Task<ActionResult<DataObjects.ComplianceReportFilterResult>> GetComplianceReports([FromBody] DataObjects.ComplianceReportFilter filter)
    {
        return Ok(await da.GetComplianceReportsAsync(filter));
    }

    [HttpPost("api/Data/GetComplianceReport")]
    public async Task<ActionResult<DataObjects.ComplianceReport?>> GetComplianceReport([FromBody] Guid id)
    {
        var item = await da.GetComplianceReportAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpGet("api/Data/GetComplianceReportLookups")]
    public async Task<ActionResult<List<DataObjects.ComplianceReportLookup>>> GetComplianceReportLookups()
    {
        return Ok(await da.GetComplianceReportLookupsAsync());
    }

    [HttpPost("api/Data/SaveComplianceReport")]
    public async Task<ActionResult<DataObjects.ComplianceReport?>> SaveComplianceReport([FromBody] DataObjects.ComplianceReport item)
    {
        var result = await da.SaveComplianceReportAsync(item);
        if (result == null) return BadRequest();
        return Ok(result);
    }

    [HttpPost("api/Data/DeleteComplianceReport")]
    public async Task<ActionResult<bool>> DeleteComplianceReport([FromBody] Guid id)
    {
        return Ok(await da.DeleteComplianceReportAsync(id));
    }

    #endregion

    #region Accessor Endpoints

    /// <summary>Get filtered list of accessors (users who have accessed data).</summary>
    [HttpPost("api/Data/GetAccessors")]
    public async Task<ActionResult<DataObjects.AccessorFilterResult>> GetAccessors([FromBody] DataObjects.AccessorFilter filter)
    {
        return Ok(await da.GetAccessorsAsync(filter));
    }

    /// <summary>Get top accessors for dashboard.</summary>
    [HttpGet("api/Data/GetTopAccessors")]
    public async Task<ActionResult<List<DataObjects.AccessorSummary>>> GetTopAccessors([FromQuery] int limit = 10)
    {
        return Ok(await da.GetTopAccessorsAsync(limit));
    }

    #endregion

    // ============================================================================
    // API REQUEST LOGGING ENDPOINTS
    // ============================================================================

    #region API Request Logging

    /// <summary>Get dashboard statistics for API logs.</summary>
    [HttpPost("api/Data/GetApiLogDashboardStats")]
    [SkipApiLogging(Reason = "Prevents infinite loop")]
    public async Task<ActionResult<DataObjects.ApiLogDashboardStats>> GetApiLogDashboardStats([FromBody] DataObjects.ApiLogDashboardRequest request)
    {
        var from = request.From ?? DateTime.UtcNow.AddHours(-24);
        var to = request.To ?? DateTime.UtcNow;
        return Ok(await da.GetApiLogDashboardStatsAsync(from, to));
    }

    /// <summary>Get paginated/filtered list of API request logs.</summary>
    [HttpPost("api/Data/GetApiLogs")]
    [SkipApiLogging(Reason = "Prevents infinite loop")]
    public async Task<ActionResult<DataObjects.ApiLogFilterResult>> GetApiLogs([FromBody] DataObjects.ApiLogFilter filter)
    {
        return Ok(await da.GetApiLogsAsync(filter));
    }

    /// <summary>Get a single API request log by ID.</summary>
    [HttpPost("api/Data/GetApiLog")]
    [SkipApiLogging(Reason = "Prevents infinite loop")]
    public async Task<ActionResult<DataObjects.ApiRequestLog?>> GetApiLog([FromBody] Guid id)
    {
        var item = await da.GetApiLogAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    #endregion

    #region Body Logging Configuration

    /// <summary>Get all body logging configurations.</summary>
    [HttpGet("api/Data/GetBodyLoggingConfigs")]
    [SkipApiLogging(Reason = "Prevents infinite loop")]
    public async Task<ActionResult<List<DataObjects.BodyLoggingConfig>>> GetBodyLoggingConfigs()
    {
        return Ok(await da.GetBodyLoggingConfigsAsync());
    }

    /// <summary>Enable body logging for a source system.</summary>
    [HttpPost("api/Data/EnableBodyLogging")]
    [SkipApiLogging(Reason = "Prevents infinite loop")]
    public async Task<ActionResult<DataObjects.BodyLoggingConfig>> EnableBodyLogging([FromBody] DataObjects.EnableBodyLoggingRequest request)
    {
        var result = await da.EnableBodyLoggingAsync(
            request.SourceSystemId,
            request.EnabledByUserId,
            request.EnabledByUserName,
            request.DurationHours,
            request.Reason);
        return Ok(result);
    }

    /// <summary>Disable body logging for a source system.</summary>
    [HttpPost("api/Data/DisableBodyLogging")]
    [SkipApiLogging(Reason = "Prevents infinite loop")]
    public async Task<ActionResult<bool>> DisableBodyLogging([FromBody] Guid configId)
    {
        return Ok(await da.DisableBodyLoggingAsync(configId));
    }

    #endregion

}

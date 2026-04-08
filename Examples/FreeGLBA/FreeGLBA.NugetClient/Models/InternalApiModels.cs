using System.Text.Json.Serialization;

namespace FreeGLBA.Client;

/// <summary>
/// Dashboard statistics returned from GET /api/glba/stats/summary.
/// </summary>
public class GlbaStats
{
    /// <summary>Total events logged today.</summary>
    [JsonPropertyName("today")]
    public long Today { get; set; }

    /// <summary>Total events logged this week.</summary>
    [JsonPropertyName("thisWeek")]
    public long ThisWeek { get; set; }

    /// <summary>Total events logged this month.</summary>
    [JsonPropertyName("thisMonth")]
    public long ThisMonth { get; set; }

    /// <summary>Total number of data subjects in the system.</summary>
    [JsonPropertyName("totalSubjects")]
    public long TotalSubjects { get; set; }

    /// <summary>Subjects accessed today.</summary>
    [JsonPropertyName("subjectsToday")]
    public long SubjectsToday { get; set; }

    /// <summary>Subjects accessed this week.</summary>
    [JsonPropertyName("subjectsThisWeek")]
    public long SubjectsThisWeek { get; set; }

    /// <summary>Subjects accessed this month.</summary>
    [JsonPropertyName("subjectsThisMonth")]
    public long SubjectsThisMonth { get; set; }

    /// <summary>Total unique accessors (users who have accessed data).</summary>
    [JsonPropertyName("totalAccessors")]
    public long TotalAccessors { get; set; }

    /// <summary>Event count by data category.</summary>
    [JsonPropertyName("byCategory")]
    public Dictionary<string, long> ByCategory { get; set; } = new();

    /// <summary>Event count by access type (View, Export, etc.).</summary>
    [JsonPropertyName("byAccessType")]
    public Dictionary<string, long> ByAccessType { get; set; } = new();
}

/// <summary>
/// Access event returned from internal API endpoints.
/// </summary>
public class AccessEvent
{
    /// <summary>Primary key.</summary>
    [JsonPropertyName("accessEventId")]
    public Guid AccessEventId { get; set; }

    /// <summary>The source system that reported this event.</summary>
    [JsonPropertyName("sourceSystemId")]
    public Guid SourceSystemId { get; set; }

    /// <summary>Deduplication key from source system.</summary>
    [JsonPropertyName("sourceEventId")]
    public string SourceEventId { get; set; } = string.Empty;

    /// <summary>When the data access occurred.</summary>
    [JsonPropertyName("accessedAt")]
    public DateTime AccessedAt { get; set; }

    /// <summary>When the event was received by FreeGLBA.</summary>
    [JsonPropertyName("receivedAt")]
    public DateTime ReceivedAt { get; set; }

    /// <summary>ID of the user who accessed the data.</summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display name of the user.</summary>
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Email of the user.</summary>
    [JsonPropertyName("userEmail")]
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>Department of the user.</summary>
    [JsonPropertyName("userDepartment")]
    public string UserDepartment { get; set; } = string.Empty;

    /// <summary>Primary subject ID or "BULK" for multi-subject access.</summary>
    [JsonPropertyName("subjectId")]
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>Type of subject (Student, Employee, etc.).</summary>
    [JsonPropertyName("subjectType")]
    public string SubjectType { get; set; } = string.Empty;

    /// <summary>JSON array of all subject IDs for bulk access.</summary>
    [JsonPropertyName("subjectIds")]
    public string SubjectIds { get; set; } = string.Empty;

    /// <summary>Count of subjects accessed.</summary>
    [JsonPropertyName("subjectCount")]
    public int SubjectCount { get; set; } = 1;

    /// <summary>Category of data accessed.</summary>
    [JsonPropertyName("dataCategory")]
    public string DataCategory { get; set; } = string.Empty;

    /// <summary>Type of access (View, Export, Print, etc.).</summary>
    [JsonPropertyName("accessType")]
    public string AccessType { get; set; } = string.Empty;

    /// <summary>Business purpose for the access.</summary>
    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    /// <summary>IP address of the client.</summary>
    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>JSON for flexible extras.</summary>
    [JsonPropertyName("additionalData")]
    public string AdditionalData { get; set; } = string.Empty;

    /// <summary>Display name of the source system.</summary>
    [JsonPropertyName("sourceSystemName")]
    public string SourceSystemName { get; set; } = string.Empty;

    /// <summary>Copy of the privacy notice shown to user.</summary>
    [JsonPropertyName("agreementText")]
    public string AgreementText { get; set; } = string.Empty;

    /// <summary>When user acknowledged the privacy agreement.</summary>
    [JsonPropertyName("agreementAcknowledgedAt")]
    public DateTime? AgreementAcknowledgedAt { get; set; }
}

/// <summary>
/// Source system status information.
/// </summary>
public class SourceSystemStatus
{
    /// <summary>Primary key.</summary>
    [JsonPropertyName("sourceSystemId")]
    public Guid SourceSystemId { get; set; }

    /// <summary>Internal name of the source system.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Display name of the source system.</summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Contact email for the source system administrator.</summary>
    [JsonPropertyName("contactEmail")]
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Whether the source system is active.</summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>When the last event was received from this source.</summary>
    [JsonPropertyName("lastEventReceivedAt")]
    public DateTime? LastEventReceivedAt { get; set; }

    /// <summary>Total event count from this source.</summary>
    [JsonPropertyName("eventCount")]
    public long EventCount { get; set; }
}

/// <summary>
/// Summary of a data accessor (user who has accessed protected data).
/// </summary>
public class AccessorSummary
{
    /// <summary>User ID of the accessor.</summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Display name of the accessor.</summary>
    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    /// <summary>Email of the accessor.</summary>
    [JsonPropertyName("userEmail")]
    public string? UserEmail { get; set; }

    /// <summary>Department of the accessor.</summary>
    [JsonPropertyName("userDepartment")]
    public string? UserDepartment { get; set; }

    /// <summary>Total number of access events by this user.</summary>
    [JsonPropertyName("totalAccesses")]
    public int TotalAccesses { get; set; }

    /// <summary>Number of unique subjects this user has accessed.</summary>
    [JsonPropertyName("uniqueSubjectsAccessed")]
    public int UniqueSubjectsAccessed { get; set; }

    /// <summary>Number of export operations by this user.</summary>
    [JsonPropertyName("exportCount")]
    public int ExportCount { get; set; }

    /// <summary>Number of view operations by this user.</summary>
    [JsonPropertyName("viewCount")]
    public int ViewCount { get; set; }

    /// <summary>When this user first accessed protected data.</summary>
    [JsonPropertyName("firstAccessAt")]
    public DateTime FirstAccessAt { get; set; }

    /// <summary>When this user last accessed protected data.</summary>
    [JsonPropertyName("lastAccessAt")]
    public DateTime LastAccessAt { get; set; }
}

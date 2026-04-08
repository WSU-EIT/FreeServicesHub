namespace FreeGLBA.Controllers;

// ============================================================================
// API REQUEST LOGGING - Skip Marker Attribute
// Marks actions/controllers that should NOT be logged
// ============================================================================

/// <summary>
/// Marker attribute to skip API request logging on specific actions.
/// Use on endpoints that would create recursive logging (e.g., log viewing endpoints)
/// or endpoints that should not be logged for other reasons.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SkipApiLoggingAttribute : Attribute
{
    /// <summary>
    /// Optional reason why logging is skipped for this endpoint.
    /// </summary>
    public string? Reason { get; set; }

    public SkipApiLoggingAttribute() { }

    public SkipApiLoggingAttribute(string reason)
    {
        Reason = reason;
    }
}

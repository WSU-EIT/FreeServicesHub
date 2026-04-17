using System;

namespace FreeServicesHub.EFModels.EFModels;

public partial class CiCdTokenUsage
{
    public Guid CiCdTokenUsageId { get; set; }
    public Guid TenantId { get; set; }
    public string TokenHash { get; set; } = null!;
    public string? TokenPrefix { get; set; }
    public int MaxUses { get; set; }
    public int UsesConsumed { get; set; }
    public bool Active { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime Created { get; set; }
    public DateTime? InvalidatedAt { get; set; }
}

using System;
using System.Collections.Generic;

namespace FreeServicesHub.EFModels.EFModels;

public partial class RegistrationKey
{
    public Guid RegistrationKeyId { get; set; }

    public Guid TenantId { get; set; }

    public string KeyHash { get; set; } = null!;

    public string? KeyPrefix { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool Used { get; set; }

    public Guid? UsedByAgentId { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime Created { get; set; }

    public string? CreatedBy { get; set; }
}

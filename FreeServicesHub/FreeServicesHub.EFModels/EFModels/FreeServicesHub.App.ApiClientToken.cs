using System;
using System.Collections.Generic;

namespace FreeServicesHub.EFModels.EFModels;

public partial class ApiClientToken
{
    public Guid ApiClientTokenId { get; set; }

    public Guid AgentId { get; set; }

    public Guid TenantId { get; set; }

    public string TokenHash { get; set; } = null!;

    public string? TokenPrefix { get; set; }

    public bool Active { get; set; }

    public DateTime Created { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedBy { get; set; }
}

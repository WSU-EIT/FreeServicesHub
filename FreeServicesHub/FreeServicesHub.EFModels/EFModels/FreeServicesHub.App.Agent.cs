using System;
using System.Collections.Generic;

namespace FreeServicesHub.EFModels.EFModels;

public partial class Agent
{
    public Guid AgentId { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string? Hostname { get; set; }

    public string? OperatingSystem { get; set; }

    public string? Architecture { get; set; }

    public string? AgentVersion { get; set; }

    public string? DotNetVersion { get; set; }

    public string? Status { get; set; }

    public DateTime? LastHeartbeat { get; set; }

    public DateTime? RegisteredAt { get; set; }

    public string? RegisteredBy { get; set; }

    public DateTime Added { get; set; }

    public string? AddedBy { get; set; }

    public DateTime LastModified { get; set; }

    public string? LastModifiedBy { get; set; }

    public bool Deleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}

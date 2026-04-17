using System;
using System.Collections.Generic;

namespace FreeServicesHub.EFModels.EFModels;

public partial class HubJob
{
    public Guid HubJobId { get; set; }

    public Guid TenantId { get; set; }

    public Guid? AgentId { get; set; }

    public string JobType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Payload { get; set; }

    public string? Result { get; set; }

    public string? ErrorMessage { get; set; }

    public int Priority { get; set; }

    public int MaxRetries { get; set; }

    public int RetryCount { get; set; }

    public DateTime Created { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime LastModified { get; set; }

    public string? LastModifiedBy { get; set; }

    public bool Deleted { get; set; }

    public DateTime? DeletedAt { get; set; }
}

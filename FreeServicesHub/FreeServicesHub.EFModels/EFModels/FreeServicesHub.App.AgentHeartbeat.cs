using System;
using System.Collections.Generic;

namespace FreeServicesHub.EFModels.EFModels;

public partial class AgentHeartbeat
{
    public Guid HeartbeatId { get; set; }

    public Guid AgentId { get; set; }

    public DateTime Timestamp { get; set; }

    public double CpuPercent { get; set; }

    public double MemoryPercent { get; set; }

    public double MemoryUsedGB { get; set; }

    public double MemoryTotalGB { get; set; }

    public string? DiskMetricsJson { get; set; }

    public string? CustomDataJson { get; set; }
}

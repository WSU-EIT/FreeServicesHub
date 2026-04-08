namespace FreeServicesHub;

public partial interface IDataAccess
{
    Task<DataObjects.BooleanResponse> SaveHeartbeat(DataObjects.AgentHeartbeat Heartbeat);
    Task<List<DataObjects.AgentHeartbeat>> GetHeartbeats(Guid AgentId, int Hours = 24);
    Task<DataObjects.BooleanResponse> PruneHeartbeats(int RetentionHours);
}

public partial class DataAccess
{
    public async Task<DataObjects.BooleanResponse> SaveHeartbeat(DataObjects.AgentHeartbeat Heartbeat)
    {
        DataObjects.BooleanResponse output = new();

        DateTime now = DateTime.UtcNow;

        try {
            if (Heartbeat.HeartbeatId == Guid.Empty) {
                Heartbeat.HeartbeatId = Guid.NewGuid();
            }

            if (Heartbeat.Timestamp == default) {
                Heartbeat.Timestamp = now;
            }

            EFModels.EFModels.AgentHeartbeat rec = new() {
                HeartbeatId = Heartbeat.HeartbeatId,
                AgentId = Heartbeat.AgentId,
                Timestamp = Heartbeat.Timestamp,
                CpuPercent = Heartbeat.CpuPercent,
                MemoryPercent = Heartbeat.MemoryPercent,
                MemoryUsedGB = Heartbeat.MemoryUsedGB,
                MemoryTotalGB = Heartbeat.MemoryTotalGB,
                DiskMetricsJson = Heartbeat.DiskMetricsJson,
                CustomDataJson = Heartbeat.CustomDataJson,
            };

            await data.AgentHeartbeats.AddAsync(rec);
            await data.SaveChangesAsync();

            // Update Agent record with latest heartbeat time and status
            EFModels.EFModels.Agent? agent = await data.Agents.FirstOrDefaultAsync(x => x.AgentId == Heartbeat.AgentId);

            if (agent != null) {
                agent.LastHeartbeat = now;

                // Determine status based on thresholds
                int cpuWarning = ConfigurationHelper?.CpuWarningThreshold ?? 70;
                int cpuError = ConfigurationHelper?.CpuErrorThreshold ?? 90;
                int memWarning = ConfigurationHelper?.MemoryWarningThreshold ?? 70;
                int memError = ConfigurationHelper?.MemoryErrorThreshold ?? 90;

                if (Heartbeat.CpuPercent >= cpuError || Heartbeat.MemoryPercent >= memError) {
                    agent.Status = DataObjects.AgentStatuses.Error;
                } else if (Heartbeat.CpuPercent >= cpuWarning || Heartbeat.MemoryPercent >= memWarning) {
                    agent.Status = DataObjects.AgentStatuses.Warning;
                } else {
                    agent.Status = DataObjects.AgentStatuses.Online;
                }

                agent.LastModified = now;

                await data.SaveChangesAsync();

                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    TenantId = agent.TenantId,
                    ItemId = agent.AgentId,
                    UpdateType = DataObjects.SignalRUpdateType.AgentHeartbeat,
                    Message = agent.Status,
                    Object = Heartbeat,
                });
            }

            output.Result = true;
        } catch (Exception ex) {
            output.Messages.Add("Error Saving Heartbeat");
            output.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }

    public async Task<List<DataObjects.AgentHeartbeat>> GetHeartbeats(Guid AgentId, int Hours = 24)
    {
        List<DataObjects.AgentHeartbeat> output = new();

        DateTime cutoff = DateTime.UtcNow.AddHours(-Hours);

        List<EFModels.EFModels.AgentHeartbeat> recs = await data.AgentHeartbeats
            .Where(x => x.AgentId == AgentId && x.Timestamp >= cutoff)
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync();

        if (recs.Any()) {
            // Load agent name for display
            EFModels.EFModels.Agent? agent = await data.Agents.FirstOrDefaultAsync(x => x.AgentId == AgentId);

            foreach (EFModels.EFModels.AgentHeartbeat rec in recs) {
                output.Add(new DataObjects.AgentHeartbeat {
                    HeartbeatId = rec.HeartbeatId,
                    AgentId = rec.AgentId,
                    Timestamp = rec.Timestamp,
                    CpuPercent = rec.CpuPercent,
                    MemoryPercent = rec.MemoryPercent,
                    MemoryUsedGB = rec.MemoryUsedGB,
                    MemoryTotalGB = rec.MemoryTotalGB,
                    DiskMetricsJson = rec.DiskMetricsJson ?? string.Empty,
                    CustomDataJson = rec.CustomDataJson ?? string.Empty,
                    AgentName = agent?.Name ?? string.Empty,
                });
            }
        }

        return output;
    }

    public async Task<DataObjects.BooleanResponse> PruneHeartbeats(int RetentionHours)
    {
        DataObjects.BooleanResponse output = new();

        try {
            DateTime cutoff = DateTime.UtcNow.AddHours(-RetentionHours);

            List<EFModels.EFModels.AgentHeartbeat> oldRecords = await data.AgentHeartbeats
                .Where(x => x.Timestamp < cutoff)
                .ToListAsync();

            if (oldRecords.Any()) {
                data.AgentHeartbeats.RemoveRange(oldRecords);
                await data.SaveChangesAsync();
            }

            output.Result = true;
        } catch (Exception ex) {
            output.Messages.Add("Error Pruning Heartbeats");
            output.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }
}

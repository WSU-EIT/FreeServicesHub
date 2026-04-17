namespace FreeServicesHub;

public partial interface IDataAccess
{
    Task<List<DataObjects.Agent>> GetAgents(List<Guid>? Ids, Guid TenantId, DataObjects.User? CurrentUser = null);
    Task<List<DataObjects.Agent>> SaveAgents(List<DataObjects.Agent> Items, DataObjects.User? CurrentUser = null);
    Task<DataObjects.BooleanResponse> DeleteAgents(List<Guid>? Ids, DataObjects.User? CurrentUser = null);
}

public partial class DataAccess
{
    public async Task<List<DataObjects.Agent>> GetAgents(List<Guid>? Ids, Guid TenantId, DataObjects.User? CurrentUser = null)
    {
        List<DataObjects.Agent> output = new();

        List<EFModels.EFModels.Agent>? recs = null;

        if (Ids != null && Ids.Any()) {
            if (AdminUser(CurrentUser)) {
                recs = await data.Agents.Where(x => x.TenantId == TenantId && Ids.Contains(x.AgentId)).ToListAsync();
            } else {
                recs = await data.Agents.Where(x => x.TenantId == TenantId && Ids.Contains(x.AgentId) && x.Deleted != true).ToListAsync();
            }
        } else {
            if (AdminUser(CurrentUser)) {
                recs = await data.Agents.Where(x => x.TenantId == TenantId).ToListAsync();
            } else {
                recs = await data.Agents.Where(x => x.TenantId == TenantId && x.Deleted != true).ToListAsync();
            }
        }

        if (recs != null && recs.Any()) {
            foreach (EFModels.EFModels.Agent rec in recs) {
                output.Add(new DataObjects.Agent {
                    ActionResponse = GetNewActionResponse(true),
                    AgentId = rec.AgentId,
                    TenantId = rec.TenantId,
                    Name = rec.Name,
                    Hostname = rec.Hostname ?? string.Empty,
                    OperatingSystem = rec.OperatingSystem ?? string.Empty,
                    Architecture = rec.Architecture ?? string.Empty,
                    AgentVersion = rec.AgentVersion ?? string.Empty,
                    DotNetVersion = rec.DotNetVersion ?? string.Empty,
                    Status = rec.Status ?? string.Empty,
                    LastHeartbeat = rec.LastHeartbeat,
                    RegisteredAt = rec.RegisteredAt,
                    RegisteredBy = rec.RegisteredBy ?? string.Empty,
                    Added = rec.Added,
                    AddedBy = LastModifiedDisplayName(rec.AddedBy),
                    LastModified = rec.LastModified,
                    LastModifiedBy = LastModifiedDisplayName(rec.LastModifiedBy),
                    Deleted = rec.Deleted,
                    DeletedAt = rec.DeletedAt,
                });
            }
        }

        return output;
    }

    public async Task<List<DataObjects.Agent>> SaveAgents(List<DataObjects.Agent> Items, DataObjects.User? CurrentUser = null)
    {
        List<DataObjects.Agent> output = new();

        foreach (DataObjects.Agent item in Items) {
            DataObjects.Agent saved = await SaveAgent(item, CurrentUser);
            output.Add(saved);
        }

        return output;
    }

    private async Task<DataObjects.Agent> SaveAgent(DataObjects.Agent agent, DataObjects.User? CurrentUser = null)
    {
        DataObjects.Agent output = agent;
        output.ActionResponse = GetNewActionResponse();

        bool newRecord = false;
        DateTime now = DateTime.UtcNow;

        EFModels.EFModels.Agent? rec = await data.Agents.FirstOrDefaultAsync(x => x.AgentId == output.AgentId);

        if (rec != null && rec.Deleted) {
            if (AdminUser(CurrentUser)) {
                // Ok to edit this record that is marked as deleted.
            } else {
                output.ActionResponse.Messages.Add("Agent '" + output.AgentId.ToString() + "' No Longer Exists");
                return output;
            }
        }

        if (rec == null) {
            if (output.AgentId == Guid.Empty) {
                newRecord = true;
                output.AgentId = Guid.NewGuid();

                rec = new EFModels.EFModels.Agent {
                    AgentId = output.AgentId,
                    TenantId = output.TenantId,
                    Deleted = false,
                    Added = now,
                    AddedBy = CurrentUserIdString(CurrentUser),
                };
            } else {
                output.ActionResponse.Messages.Add("Agent '" + output.AgentId.ToString() + "' No Longer Exists");
                return output;
            }
        }

        output.Name = MaxStringLength(output.Name, 255);
        output.Hostname = MaxStringLength(output.Hostname, 255);

        rec.Name = output.Name;
        rec.Hostname = output.Hostname;
        rec.OperatingSystem = output.OperatingSystem;
        rec.Architecture = output.Architecture;
        rec.AgentVersion = output.AgentVersion;
        rec.DotNetVersion = output.DotNetVersion;
        rec.Status = output.Status;
        rec.LastHeartbeat = output.LastHeartbeat;
        rec.RegisteredAt = output.RegisteredAt;
        rec.RegisteredBy = output.RegisteredBy;
        rec.LastModified = now;
        rec.LastModifiedBy = CurrentUserIdString(CurrentUser);

        if (AdminUser(CurrentUser)) {
            rec.Deleted = output.Deleted;

            if (!output.Deleted) {
                rec.DeletedAt = null;
            }
        }

        try {
            if (newRecord) {
                await data.Agents.AddAsync(rec);
            }
            await data.SaveChangesAsync();

            output.ActionResponse.Result = true;

            await SignalRUpdate(new DataObjects.SignalRUpdate {
                TenantId = output.TenantId,
                ItemId = output.AgentId,
                UpdateType = DataObjects.SignalRUpdateType.AgentStatusChanged,
                Message = "Saved",
                UserId = CurrentUserId(CurrentUser),
                Object = output,
            });
        } catch (Exception ex) {
            output.ActionResponse.Messages.Add("Error Saving Agent " + output.AgentId.ToString());
            output.ActionResponse.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }

    public async Task<DataObjects.BooleanResponse> DeleteAgents(List<Guid>? Ids, DataObjects.User? CurrentUser = null)
    {
        DataObjects.BooleanResponse output = new();

        if (Ids == null || !Ids.Any()) {
            output.Messages.Add("No Agent Ids provided for deletion.");
            return output;
        }

        DateTime now = DateTime.UtcNow;

        try {
            List<EFModels.EFModels.Agent> recs = await data.Agents.Where(x => Ids.Contains(x.AgentId)).ToListAsync();

            if (!recs.Any()) {
                output.Messages.Add("No matching Agent records found.");
                return output;
            }

            foreach (EFModels.EFModels.Agent rec in recs) {
                rec.Deleted = true;
                rec.DeletedAt = now;
                rec.LastModified = now;

                if (CurrentUser != null) {
                    rec.LastModifiedBy = CurrentUserIdString(CurrentUser);
                }
            }

            // Cascade soft-delete: mark any jobs assigned to these agents as Cancelled
            var agentIds = recs.Select(r => r.AgentId).ToList();
            var orphanedJobs = await data.HubJobs
                .Where(j => j.AgentId.HasValue && agentIds.Contains(j.AgentId.Value) && !j.Deleted
                    && j.Status != "Completed" && j.Status != "Failed" && j.Status != "Cancelled")
                .ToListAsync();
            foreach (var job in orphanedJobs) {
                job.Status = "Cancelled";
                job.ErrorMessage = "Agent deleted";
                job.CompletedAt = now;
                job.LastModified = now;
            }

            await data.SaveChangesAsync();
            output.Result = true;

            foreach (EFModels.EFModels.Agent rec in recs) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    TenantId = rec.TenantId,
                    ItemId = rec.AgentId,
                    UpdateType = DataObjects.SignalRUpdateType.AgentStatusChanged,
                    Message = "Deleted",
                    UserId = CurrentUserId(CurrentUser),
                });
            }
        } catch (Exception ex) {
            output.Messages.Add("Error Deleting Agents");
            output.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }
}

namespace FreeServicesHub;

public partial interface IDataAccess
{
    Task<List<DataObjects.HubJob>> GetHubJobs(List<Guid>? Ids, Guid TenantId, DataObjects.User? CurrentUser = null);
    Task<List<DataObjects.HubJob>> GetJobsForAgent(Guid AgentId, Guid TenantId);
    Task<List<DataObjects.HubJob>> SaveHubJobs(List<DataObjects.HubJob> Items, DataObjects.User? CurrentUser = null);
    Task<DataObjects.BooleanResponse> DeleteHubJobs(List<Guid>? Ids, DataObjects.User? CurrentUser = null);
}

public partial class DataAccess
{
    public async Task<List<DataObjects.HubJob>> GetHubJobs(List<Guid>? Ids, Guid TenantId, DataObjects.User? CurrentUser = null)
    {
        List<DataObjects.HubJob> output = new();

        List<EFModels.EFModels.HubJob>? recs = null;

        if (Ids != null && Ids.Any()) {
            if (AdminUser(CurrentUser)) {
                recs = await data.HubJobs.Where(x => x.TenantId == TenantId && Ids.Contains(x.HubJobId)).ToListAsync();
            } else {
                recs = await data.HubJobs.Where(x => x.TenantId == TenantId && Ids.Contains(x.HubJobId) && x.Deleted != true).ToListAsync();
            }
        } else {
            if (AdminUser(CurrentUser)) {
                recs = await data.HubJobs.Where(x => x.TenantId == TenantId).ToListAsync();
            } else {
                recs = await data.HubJobs.Where(x => x.TenantId == TenantId && x.Deleted != true).ToListAsync();
            }
        }

        if (recs != null && recs.Any()) {
            foreach (EFModels.EFModels.HubJob rec in recs) {
                output.Add(new DataObjects.HubJob {
                    ActionResponse = GetNewActionResponse(true),
                    HubJobId = rec.HubJobId,
                    TenantId = rec.TenantId,
                    AgentId = rec.AgentId,
                    JobType = rec.JobType,
                    Status = rec.Status ?? string.Empty,
                    Payload = rec.Payload ?? string.Empty,
                    Result = rec.Result ?? string.Empty,
                    ErrorMessage = rec.ErrorMessage ?? string.Empty,
                    Priority = rec.Priority,
                    MaxRetries = rec.MaxRetries,
                    RetryCount = rec.RetryCount,
                    Created = rec.Created,
                    CreatedBy = LastModifiedDisplayName(rec.CreatedBy),
                    StartedAt = rec.StartedAt,
                    CompletedAt = rec.CompletedAt,
                    LastModified = rec.LastModified,
                    LastModifiedBy = LastModifiedDisplayName(rec.LastModifiedBy),
                    Deleted = rec.Deleted,
                    DeletedAt = rec.DeletedAt,
                });
            }
        }

        return output;
    }

    public async Task<List<DataObjects.HubJob>> GetJobsForAgent(Guid AgentId, Guid TenantId)
    {
        List<DataObjects.HubJob> output = new();

        var recs = await data.HubJobs
            .Where(x => x.TenantId == TenantId && !x.Deleted
                && (x.AgentId == AgentId || (x.Status == "Queued" && x.AgentId == null)))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Created)
            .ToListAsync();

        foreach (EFModels.EFModels.HubJob rec in recs) {
            output.Add(new DataObjects.HubJob {
                ActionResponse = GetNewActionResponse(true),
                HubJobId = rec.HubJobId,
                TenantId = rec.TenantId,
                AgentId = rec.AgentId,
                JobType = rec.JobType,
                Status = rec.Status ?? string.Empty,
                Payload = rec.Payload ?? string.Empty,
                Result = rec.Result ?? string.Empty,
                ErrorMessage = rec.ErrorMessage ?? string.Empty,
                Priority = rec.Priority,
                MaxRetries = rec.MaxRetries,
                RetryCount = rec.RetryCount,
                Created = rec.Created,
                CreatedBy = LastModifiedDisplayName(rec.CreatedBy),
                StartedAt = rec.StartedAt,
                CompletedAt = rec.CompletedAt,
                LastModified = rec.LastModified,
                LastModifiedBy = LastModifiedDisplayName(rec.LastModifiedBy),
                Deleted = rec.Deleted,
                DeletedAt = rec.DeletedAt,
            });
        }

        return output;
    }

    public async Task<List<DataObjects.HubJob>> SaveHubJobs(List<DataObjects.HubJob> Items, DataObjects.User? CurrentUser = null)
    {
        List<DataObjects.HubJob> output = new();

        foreach (DataObjects.HubJob item in Items) {
            DataObjects.HubJob saved = await SaveHubJob(item, CurrentUser);
            output.Add(saved);
        }

        return output;
    }

    private async Task<DataObjects.HubJob> SaveHubJob(DataObjects.HubJob job, DataObjects.User? CurrentUser = null)
    {
        DataObjects.HubJob output = job;
        output.ActionResponse = GetNewActionResponse();

        bool newRecord = false;
        DateTime now = DateTime.UtcNow;

        EFModels.EFModels.HubJob? rec = await data.HubJobs.FirstOrDefaultAsync(x => x.HubJobId == output.HubJobId);

        if (rec != null && rec.Deleted) {
            if (AdminUser(CurrentUser)) {
                // Ok to edit this record that is marked as deleted.
            } else {
                output.ActionResponse.Messages.Add("Job '" + output.HubJobId.ToString() + "' No Longer Exists");
                return output;
            }
        }

        string? previousStatus = rec?.Status;

        if (rec == null) {
            if (output.HubJobId == Guid.Empty) {
                newRecord = true;
                output.HubJobId = Guid.NewGuid();

                rec = new EFModels.EFModels.HubJob {
                    HubJobId = output.HubJobId,
                    TenantId = output.TenantId,
                    Deleted = false,
                    Created = now,
                    CreatedBy = CurrentUserIdString(CurrentUser),
                };
            } else {
                output.ActionResponse.Messages.Add("Job '" + output.HubJobId.ToString() + "' No Longer Exists");
                return output;
            }
        }

        output.JobType = MaxStringLength(output.JobType, 100);
        output.Status = MaxStringLength(output.Status, 50);

        rec.AgentId = output.AgentId;
        rec.JobType = output.JobType;
        rec.Status = output.Status;
        rec.Payload = output.Payload;
        rec.Result = output.Result;
        rec.ErrorMessage = output.ErrorMessage;
        rec.Priority = output.Priority;
        rec.MaxRetries = output.MaxRetries;
        rec.RetryCount = output.RetryCount;
        rec.StartedAt = output.StartedAt;
        rec.CompletedAt = output.CompletedAt;
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
                await data.HubJobs.AddAsync(rec);
            }
            await data.SaveChangesAsync();

            output.ActionResponse.Result = true;

            string updateType = DataObjects.SignalRUpdateType.JobUpdated;
            if (output.Status == DataObjects.HubJobStatuses.Completed
                || output.Status == DataObjects.HubJobStatuses.Failed
                || output.Status == DataObjects.HubJobStatuses.Cancelled) {
                updateType = DataObjects.SignalRUpdateType.JobCompleted;
            }

            await SignalRUpdate(new DataObjects.SignalRUpdate {
                TenantId = output.TenantId,
                ItemId = output.HubJobId,
                UpdateType = updateType,
                Message = "Saved",
                UserId = CurrentUserId(CurrentUser),
                Object = output,
            });
        } catch (Exception ex) {
            output.ActionResponse.Messages.Add("Error Saving Job " + output.HubJobId.ToString());
            output.ActionResponse.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }

    public async Task<DataObjects.BooleanResponse> DeleteHubJobs(List<Guid>? Ids, DataObjects.User? CurrentUser = null)
    {
        DataObjects.BooleanResponse output = new();

        if (Ids == null || !Ids.Any()) {
            output.Messages.Add("No Job Ids provided for deletion.");
            return output;
        }

        DateTime now = DateTime.UtcNow;

        try {
            List<EFModels.EFModels.HubJob> recs = await data.HubJobs.Where(x => Ids.Contains(x.HubJobId)).ToListAsync();

            if (!recs.Any()) {
                output.Messages.Add("No matching Job records found.");
                return output;
            }

            foreach (EFModels.EFModels.HubJob rec in recs) {
                rec.Deleted = true;
                rec.DeletedAt = now;
                rec.LastModified = now;

                if (CurrentUser != null) {
                    rec.LastModifiedBy = CurrentUserIdString(CurrentUser);
                }
            }

            await data.SaveChangesAsync();
            output.Result = true;

            foreach (EFModels.EFModels.HubJob rec in recs) {
                await SignalRUpdate(new DataObjects.SignalRUpdate {
                    TenantId = rec.TenantId,
                    ItemId = rec.HubJobId,
                    UpdateType = DataObjects.SignalRUpdateType.JobUpdated,
                    Message = "Deleted",
                    UserId = CurrentUserId(CurrentUser),
                });
            }
        } catch (Exception ex) {
            output.Messages.Add("Error Deleting Jobs");
            output.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }
}

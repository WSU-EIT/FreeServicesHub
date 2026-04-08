namespace FreeServicesHub;

public partial interface IDataAccess
{
    Task<DataObjects.AgentRegistrationResponse> RegisterAgent(DataObjects.AgentRegistrationRequest Request, Guid TenantId);
}

public partial class DataAccess
{
    public async Task<DataObjects.AgentRegistrationResponse> RegisterAgent(DataObjects.AgentRegistrationRequest Request, Guid TenantId)
    {
        DataObjects.AgentRegistrationResponse output = new();

        // Validate the registration key
        DataObjects.RegistrationKey? regKey = await ValidateRegistrationKey(Request.RegistrationKey, TenantId);

        if (regKey == null) {
            output.ActionResponse.Messages.Add("Invalid, expired, or already-used registration key.");
            return output;
        }

        DateTime now = DateTime.UtcNow;

        try {
            // Create the agent record
            Guid agentId = Guid.NewGuid();

            EFModels.EFModels.Agent agentRec = new() {
                AgentId = agentId,
                TenantId = TenantId,
                Name = Request.Hostname,
                Hostname = Request.Hostname,
                OperatingSystem = Request.OperatingSystem,
                Architecture = Request.Architecture,
                AgentVersion = Request.AgentVersion,
                DotNetVersion = Request.DotNetVersion,
                Status = DataObjects.AgentStatuses.Online,
                LastHeartbeat = now,
                RegisteredAt = now,
                RegisteredBy = "RegistrationKey:" + regKey.KeyPrefix,
                Added = now,
                AddedBy = "RegistrationKey:" + regKey.KeyPrefix,
                LastModified = now,
                LastModifiedBy = "RegistrationKey:" + regKey.KeyPrefix,
                Deleted = false,
            };

            await data.Agents.AddAsync(agentRec);
            await data.SaveChangesAsync();

            // Burn the registration key
            EFModels.EFModels.RegistrationKey? regKeyRec = await data.RegistrationKeys
                .FirstOrDefaultAsync(x => x.RegistrationKeyId == regKey.RegistrationKeyId);

            if (regKeyRec != null) {
                regKeyRec.Used = true;
                regKeyRec.UsedByAgentId = agentId;
                regKeyRec.UsedAt = now;
                await data.SaveChangesAsync();
            }

            // Generate an API client token for the agent
            DataObjects.ApiClientToken token = await GenerateApiClientToken(agentId, TenantId);

            if (!token.ActionResponse.Result) {
                output.ActionResponse.Messages.Add("Agent created but failed to generate API client token.");
                output.ActionResponse.Messages.AddRange(token.ActionResponse.Messages);
                return output;
            }

            output.ActionResponse = GetNewActionResponse(true);
            output.AgentId = agentId;
            output.ApiClientToken = token.NewTokenPlaintext;

            await SignalRUpdate(new DataObjects.SignalRUpdate {
                TenantId = TenantId,
                ItemId = agentId,
                UpdateType = DataObjects.SignalRUpdateType.AgentConnected,
                Message = "Agent Registered: " + Request.Hostname,
            });
        } catch (Exception ex) {
            output.ActionResponse.Messages.Add("Error Registering Agent");
            output.ActionResponse.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }
}

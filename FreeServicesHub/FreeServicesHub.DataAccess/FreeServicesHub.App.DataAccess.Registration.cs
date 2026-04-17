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

        // Validate the registration key (hash-only lookup, TenantId comes from the key)
        DataObjects.RegistrationKey? regKey = await ValidateRegistrationKey(Request.RegistrationKey, TenantId);

        if (regKey == null) {
            output.ActionResponse.Messages.Add("Invalid, expired, or already-used registration key.");
            return output;
        }

        // Use the key's TenantId (set during CiCd key generation) so the agent
        // lands in the correct tenant even though this endpoint is anonymous.
        Guid agentTenantId = regKey.TenantId;

        DateTime now = DateTime.UtcNow;

        try {
            // Upsert: if an agent with the same name already exists, update it
            EFModels.EFModels.Agent? agentRec = await data.Agents
                .FirstOrDefaultAsync(x => x.Name == Request.Hostname);

            Guid agentId;
            bool isNewAgent;

            if (agentRec != null) {
                // Existing agent — update in place
                agentId = agentRec.AgentId;
                isNewAgent = false;

                agentRec.TenantId = agentTenantId;
                agentRec.Hostname = Request.Hostname;
                agentRec.OperatingSystem = Request.OperatingSystem;
                agentRec.Architecture = Request.Architecture;
                agentRec.AgentVersion = Request.AgentVersion;
                agentRec.DotNetVersion = Request.DotNetVersion;
                agentRec.Status = DataObjects.AgentStatuses.Online;
                agentRec.LastHeartbeat = now;
                agentRec.RegisteredAt = now;
                agentRec.RegisteredBy = "RegistrationKey:" + regKey.KeyPrefix;
                agentRec.LastModified = now;
                agentRec.LastModifiedBy = "RegistrationKey:" + regKey.KeyPrefix;
                agentRec.Deleted = false;
                agentRec.DeletedAt = null;

                // Revoke all existing API tokens for this agent
                var oldTokens = await data.ApiClientTokens
                    .Where(x => x.AgentId == agentId && x.Active == true)
                    .ToListAsync();
                foreach (var t in oldTokens) {
                    t.Active = false;
                    t.RevokedAt = now;
                    t.RevokedBy = "Re-registration";
                }
            } else {
                // New agent
                agentId = Guid.NewGuid();
                isNewAgent = true;

                agentRec = new() {
                    AgentId = agentId,
                    TenantId = agentTenantId,
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
            }

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

            // Generate a fresh API client token for the agent
            DataObjects.ApiClientToken token = await GenerateApiClientToken(agentId, agentTenantId);

            if (!token.ActionResponse.Result) {
                output.ActionResponse.Messages.Add("Agent created but failed to generate API client token.");
                output.ActionResponse.Messages.AddRange(token.ActionResponse.Messages);
                return output;
            }

            output.ActionResponse = GetNewActionResponse(true);
            output.AgentId = agentId;
            output.ApiClientToken = token.NewTokenPlaintext;

            await SignalRUpdate(new DataObjects.SignalRUpdate {
                TenantId = agentTenantId,
                ItemId = agentId,
                UpdateType = DataObjects.SignalRUpdateType.AgentConnected,
                Message = (isNewAgent ? "Agent Registered: " : "Agent Re-registered: ") + Request.Hostname,
            });
        } catch (Exception ex) {
            output.ActionResponse.Messages.Add("Error Registering Agent");
            output.ActionResponse.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }
}

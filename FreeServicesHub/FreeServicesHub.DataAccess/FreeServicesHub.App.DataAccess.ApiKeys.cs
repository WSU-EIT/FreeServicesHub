using System.Security.Cryptography;
using System.Text;

namespace FreeServicesHub;

public partial interface IDataAccess
{
    Task<List<DataObjects.RegistrationKey>> GenerateRegistrationKeys(int Count, Guid TenantId, DataObjects.User? CurrentUser = null);
    Task<List<DataObjects.RegistrationKey>> GetRegistrationKeys(Guid TenantId);
    Task<DataObjects.RegistrationKey?> ValidateRegistrationKey(string PlaintextKey, Guid TenantId);
    Task<DataObjects.ApiClientToken> GenerateApiClientToken(Guid AgentId, Guid TenantId);
    Task<DataObjects.ApiClientToken?> ValidateApiClientToken(string PlaintextToken);
    Task<DataObjects.BooleanResponse> RevokeApiClientToken(Guid TokenId, DataObjects.User? CurrentUser = null);
    Task<List<DataObjects.ApiClientToken>> GetApiClientTokens(Guid TenantId);
}

public partial class DataAccess
{
    private static string HashKey(string plaintext)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(hash);
    }

    private static string GeneratePlaintextKey()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public async Task<List<DataObjects.RegistrationKey>> GenerateRegistrationKeys(int Count, Guid TenantId, DataObjects.User? CurrentUser = null)
    {
        List<DataObjects.RegistrationKey> output = new();

        DateTime now = DateTime.UtcNow;
        int expiryHours = ConfigurationHelper?.RegistrationKeyExpiryHours ?? 24;

        try {
            for (int i = 0; i < Count; i++) {
                string plaintext = GeneratePlaintextKey();
                string hash = HashKey(plaintext);
                string prefix = plaintext.Substring(0, 8);
                Guid keyId = Guid.NewGuid();

                EFModels.EFModels.RegistrationKey rec = new() {
                    RegistrationKeyId = keyId,
                    TenantId = TenantId,
                    KeyHash = hash,
                    KeyPrefix = prefix,
                    ExpiresAt = now.AddHours(expiryHours),
                    Used = false,
                    UsedByAgentId = null,
                    UsedAt = null,
                    Created = now,
                    CreatedBy = CurrentUserIdString(CurrentUser),
                };

                await data.RegistrationKeys.AddAsync(rec);
                await data.SaveChangesAsync();

                output.Add(new DataObjects.RegistrationKey {
                    ActionResponse = GetNewActionResponse(true),
                    RegistrationKeyId = keyId,
                    TenantId = TenantId,
                    KeyHash = hash,
                    KeyPrefix = prefix,
                    ExpiresAt = rec.ExpiresAt,
                    Used = false,
                    Created = now,
                    CreatedBy = LastModifiedDisplayName(rec.CreatedBy),
                    NewKeyPlaintext = plaintext,
                });
            }

            await SignalRUpdate(new DataObjects.SignalRUpdate {
                TenantId = TenantId,
                UpdateType = DataObjects.SignalRUpdateType.RegistrationKeyGenerated,
                Message = "Generated " + Count.ToString() + " registration key(s)",
                UserId = CurrentUserId(CurrentUser),
            });
        } catch (Exception ex) {
            DataObjects.RegistrationKey errorKey = new();
            errorKey.ActionResponse.Messages.Add("Error Generating Registration Keys");
            errorKey.ActionResponse.Messages.AddRange(RecurseException(ex));
            output.Add(errorKey);
        }

        return output;
    }

    public async Task<List<DataObjects.RegistrationKey>> GetRegistrationKeys(Guid TenantId)
    {
        List<DataObjects.RegistrationKey> output = new();

        List<EFModels.EFModels.RegistrationKey> recs = await data.RegistrationKeys
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.Created)
            .ToListAsync();

        foreach (EFModels.EFModels.RegistrationKey rec in recs) {
            output.Add(new DataObjects.RegistrationKey {
                ActionResponse = GetNewActionResponse(true),
                RegistrationKeyId = rec.RegistrationKeyId,
                TenantId = rec.TenantId,
                KeyHash = rec.KeyHash,
                KeyPrefix = rec.KeyPrefix ?? string.Empty,
                ExpiresAt = rec.ExpiresAt,
                Used = rec.Used,
                UsedByAgentId = rec.UsedByAgentId,
                UsedAt = rec.UsedAt,
                Created = rec.Created,
                CreatedBy = LastModifiedDisplayName(rec.CreatedBy ?? string.Empty),
            });
        }

        return output;
    }

    public async Task<DataObjects.RegistrationKey?> ValidateRegistrationKey(string PlaintextKey, Guid TenantId)
    {
        if (string.IsNullOrWhiteSpace(PlaintextKey)) {
            return null;
        }

        string hash = HashKey(PlaintextKey);
        DateTime now = DateTime.UtcNow;

        EFModels.EFModels.RegistrationKey? rec = await data.RegistrationKeys
            .FirstOrDefaultAsync(x => x.KeyHash == hash && x.Used == false && x.ExpiresAt > now);

        if (rec == null) {
            return null;
        }

        return new DataObjects.RegistrationKey {
            ActionResponse = GetNewActionResponse(true),
            RegistrationKeyId = rec.RegistrationKeyId,
            TenantId = rec.TenantId,
            KeyHash = rec.KeyHash,
            KeyPrefix = rec.KeyPrefix ?? string.Empty,
            ExpiresAt = rec.ExpiresAt,
            Used = rec.Used,
            UsedByAgentId = rec.UsedByAgentId,
            UsedAt = rec.UsedAt,
            Created = rec.Created,
            CreatedBy = rec.CreatedBy ?? string.Empty,
        };
    }

    public async Task<DataObjects.ApiClientToken> GenerateApiClientToken(Guid AgentId, Guid TenantId)
    {
        DataObjects.ApiClientToken output = new();

        DateTime now = DateTime.UtcNow;

        try {
            string plaintext = GeneratePlaintextKey();
            string hash = HashKey(plaintext);
            string prefix = plaintext.Substring(0, 8);
            Guid tokenId = Guid.NewGuid();

            EFModels.EFModels.ApiClientToken rec = new() {
                ApiClientTokenId = tokenId,
                AgentId = AgentId,
                TenantId = TenantId,
                TokenHash = hash,
                TokenPrefix = prefix,
                Active = true,
                Created = now,
                RevokedAt = null,
                RevokedBy = null,
            };

            await data.ApiClientTokens.AddAsync(rec);
            await data.SaveChangesAsync();

            // Look up agent name for display
            EFModels.EFModels.Agent? agent = await data.Agents.FirstOrDefaultAsync(x => x.AgentId == AgentId);

            output.ActionResponse = GetNewActionResponse(true);
            output.ApiClientTokenId = tokenId;
            output.AgentId = AgentId;
            output.TenantId = TenantId;
            output.TokenHash = hash;
            output.TokenPrefix = prefix;
            output.Active = true;
            output.Created = now;
            output.AgentName = agent?.Name ?? string.Empty;
            output.NewTokenPlaintext = plaintext;
        } catch (Exception ex) {
            output.ActionResponse.Messages.Add("Error Generating API Client Token");
            output.ActionResponse.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }

    public async Task<DataObjects.ApiClientToken?> ValidateApiClientToken(string PlaintextToken)
    {
        if (string.IsNullOrWhiteSpace(PlaintextToken)) {
            return null;
        }

        string hash = HashKey(PlaintextToken);

        EFModels.EFModels.ApiClientToken? rec = await data.ApiClientTokens
            .FirstOrDefaultAsync(x => x.TokenHash == hash && x.Active == true);

        if (rec == null) {
            return null;
        }

        EFModels.EFModels.Agent? agent = await data.Agents.FirstOrDefaultAsync(x => x.AgentId == rec.AgentId);

        return new DataObjects.ApiClientToken {
            ActionResponse = GetNewActionResponse(true),
            ApiClientTokenId = rec.ApiClientTokenId,
            AgentId = rec.AgentId,
            TenantId = rec.TenantId,
            TokenHash = rec.TokenHash,
            TokenPrefix = rec.TokenPrefix ?? string.Empty,
            Active = rec.Active,
            Created = rec.Created,
            RevokedAt = rec.RevokedAt,
            RevokedBy = rec.RevokedBy ?? string.Empty,
            AgentName = agent?.Name ?? string.Empty,
        };
    }

    public async Task<DataObjects.BooleanResponse> RevokeApiClientToken(Guid TokenId, DataObjects.User? CurrentUser = null)
    {
        DataObjects.BooleanResponse output = new();

        EFModels.EFModels.ApiClientToken? rec = await data.ApiClientTokens.FirstOrDefaultAsync(x => x.ApiClientTokenId == TokenId);

        if (rec == null) {
            output.Messages.Add("API Client Token '" + TokenId.ToString() + "' Not Found");
            return output;
        }

        try {
            rec.Active = false;
            rec.RevokedAt = DateTime.UtcNow;
            rec.RevokedBy = CurrentUserIdString(CurrentUser);

            await data.SaveChangesAsync();
            output.Result = true;
        } catch (Exception ex) {
            output.Messages.Add("Error Revoking API Client Token " + TokenId.ToString());
            output.Messages.AddRange(RecurseException(ex));
        }

        return output;
    }

    public async Task<List<DataObjects.ApiClientToken>> GetApiClientTokens(Guid TenantId)
    {
        List<DataObjects.ApiClientToken> output = new();

        List<EFModels.EFModels.ApiClientToken> recs = await data.ApiClientTokens
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.Created)
            .ToListAsync();

        if (recs.Any()) {
            // Load agent names in batch
            List<Guid> agentIds = recs.Select(x => x.AgentId).Distinct().ToList();
            List<EFModels.EFModels.Agent> agents = await data.Agents
                .Where(x => agentIds.Contains(x.AgentId))
                .ToListAsync();

            foreach (EFModels.EFModels.ApiClientToken rec in recs) {
                EFModels.EFModels.Agent? agent = agents.FirstOrDefault(x => x.AgentId == rec.AgentId);

                output.Add(new DataObjects.ApiClientToken {
                    ActionResponse = GetNewActionResponse(true),
                    ApiClientTokenId = rec.ApiClientTokenId,
                    AgentId = rec.AgentId,
                    TenantId = rec.TenantId,
                    TokenHash = rec.TokenHash,
                    TokenPrefix = rec.TokenPrefix ?? string.Empty,
                    Active = rec.Active,
                    Created = rec.Created,
                    RevokedAt = rec.RevokedAt,
                    RevokedBy = rec.RevokedBy ?? string.Empty,
                    AgentName = agent?.Name ?? string.Empty,
                });
            }
        }

        return output;
    }
}

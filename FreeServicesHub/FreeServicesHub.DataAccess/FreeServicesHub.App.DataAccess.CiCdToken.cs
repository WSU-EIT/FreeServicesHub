namespace FreeServicesHub;

public partial interface IDataAccess
{
    Task<bool> ConsumeCiCdTokenUse(string plaintextToken, Guid tenantId);
    Task<bool> InvalidateCiCdToken(string plaintextToken, Guid tenantId);
}

public partial class DataAccess
{
    /// <summary>
    /// Validates a CiCd token can still be used (has remaining uses, not expired, not invalidated).
    /// On first use, auto-creates a DB tracking record. Each call decrements remaining uses.
    /// When uses are exhausted, the token is automatically invalidated.
    /// Returns true if the use was consumed successfully.
    /// </summary>
    public async Task<bool> ConsumeCiCdTokenUse(string plaintextToken, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken)) return false;

        string hash = HashKey(plaintextToken);
        string prefix = plaintextToken.Length >= 8 ? plaintextToken.Substring(0, 8) : plaintextToken;
        DateTime now = DateTime.UtcNow;

        try {
            // Look up existing tracking record by hash
            var rec = await data.CiCdTokenUsages
                .FirstOrDefaultAsync(x => x.TokenHash == hash && x.TenantId == tenantId);

            if (rec == null) {
                // First use of this token — create tracking record
                int maxUses = ConfigurationHelper?.CiCdMaxAgentRegistrations ?? 10;

                rec = new EFModels.EFModels.CiCdTokenUsage {
                    CiCdTokenUsageId = Guid.NewGuid(),
                    TenantId = tenantId,
                    TokenHash = hash,
                    TokenPrefix = prefix,
                    MaxUses = maxUses,
                    UsesConsumed = 0,
                    Active = true,
                    ExpiresAt = now.AddHours(2), // Token valid for 2 hours (covers one pipeline run)
                    Created = now,
                };

                await data.CiCdTokenUsages.AddAsync(rec);
                await data.SaveChangesAsync();
            }

            // Check if token is still valid
            if (!rec.Active || rec.ExpiresAt <= now || rec.InvalidatedAt != null) {
                return false;
            }

            if (rec.UsesConsumed >= rec.MaxUses) {
                // Already exhausted — auto-invalidate
                rec.Active = false;
                rec.InvalidatedAt = now;
                await data.SaveChangesAsync();
                return false;
            }

            // Consume one use
            rec.UsesConsumed++;

            // Auto-invalidate if all uses consumed
            if (rec.UsesConsumed >= rec.MaxUses) {
                rec.Active = false;
                rec.InvalidatedAt = now;
            }

            await data.SaveChangesAsync();
            return true;
        } catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"ConsumeCiCdTokenUse error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Explicitly invalidates a CiCd token (called at end of pipeline to burn it).
    /// Returns true if the token was found and invalidated.
    /// </summary>
    public async Task<bool> InvalidateCiCdToken(string plaintextToken, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken)) return false;

        string hash = HashKey(plaintextToken);

        try {
            var rec = await data.CiCdTokenUsages
                .FirstOrDefaultAsync(x => x.TokenHash == hash && x.TenantId == tenantId);

            if (rec == null) return false;

            rec.Active = false;
            rec.InvalidatedAt = DateTime.UtcNow;
            await data.SaveChangesAsync();

            return true;
        } catch {
            return false;
        }
    }
}

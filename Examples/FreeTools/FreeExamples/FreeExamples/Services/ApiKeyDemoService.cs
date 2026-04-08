using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace FreeExamples.Server.Services;

/// <summary>
/// In-memory service for API key management and request logging.
/// Pattern from: FreeGLBA DataAccess.ApiKey.cs (ValidateApiKeyAsync, GenerateApiKeyAsync, HashApiKey).
/// This demo version uses in-memory storage instead of a database.
/// </summary>
public class ApiKeyDemoService
{
    private readonly ConcurrentDictionary<Guid, DataObjects.ApiKeyInfo> _keys = new();
    private readonly ConcurrentBag<DataObjects.ApiKeyRequestLog> _logs = new();
    private const int MaxLogEntries = 100;

    /// <summary>
    /// Generates a new API key. Returns the plaintext key (shown to user once).
    /// Only the SHA-256 hash is stored — mirroring the FreeGLBA pattern.
    /// </summary>
    public (DataObjects.ApiKeyInfo KeyInfo, string PlaintextKey) GenerateKey(string name)
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var plaintextKey = Convert.ToBase64String(bytes);

        var info = new DataObjects.ApiKeyInfo {
            ApiKeyId = Guid.NewGuid(),
            Name = name,
            KeyHash = HashKey(plaintextKey),
            KeyPrefix = plaintextKey[..8] + "...",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _keys[info.ApiKeyId] = info;
        return (info, plaintextKey);
    }

    /// <summary>
    /// Validates a plaintext API key by hashing it and comparing to stored hashes.
    /// Returns null if not found or revoked — same pattern as FreeGLBA.ValidateApiKeyAsync.
    /// </summary>
    public DataObjects.ApiKeyInfo? ValidateKey(string plaintextKey)
    {
        var hash = HashKey(plaintextKey);
        var match = _keys.Values.FirstOrDefault(k => k.KeyHash == hash && k.IsActive);
        if (match != null) {
            match.RequestCount++;
        }
        return match;
    }

    /// <summary>
    /// Revokes (deactivates) an API key by ID.
    /// </summary>
    public bool RevokeKey(Guid apiKeyId)
    {
        if (_keys.TryGetValue(apiKeyId, out var key)) {
            key.IsActive = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all registered keys (active and revoked).
    /// </summary>
    public List<DataObjects.ApiKeyInfo> GetKeys()
    {
        return _keys.Values.OrderByDescending(k => k.CreatedAt).ToList();
    }

    /// <summary>
    /// Logs a request that passed through the middleware.
    /// </summary>
    public void LogRequest(string path, string method, string? keyName, int statusCode, string? detail = null)
    {
        _logs.Add(new DataObjects.ApiKeyRequestLog {
            Timestamp = DateTime.UtcNow,
            Path = path,
            Method = method,
            KeyName = keyName,
            StatusCode = statusCode,
            Detail = detail,
        });
    }

    /// <summary>
    /// Gets the most recent request log entries.
    /// </summary>
    public List<DataObjects.ApiKeyRequestLog> GetLogs(int count = 50)
    {
        return _logs.OrderByDescending(l => l.Timestamp).Take(count).ToList();
    }

    /// <summary>
    /// SHA-256 hash — identical to FreeGLBA's HashApiKey method.
    /// </summary>
    private static string HashKey(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(bytes);
    }
}

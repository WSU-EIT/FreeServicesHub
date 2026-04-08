using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace FreeCICD.Server.Hubs
{
    public partial interface IsrHub
    {
        Task SignalRUpdate(DataObjects.SignalRUpdate update);
    }

    // Note: [Authorize] removed to allow connection tracking. 
    // Individual hub methods can still use [Authorize] if needed.
    // Auth is checked at the application level anyway.
    public partial class freecicdHub : Hub<IsrHub>
    {
        // Static dictionary to track all active connections
        private static readonly ConcurrentDictionary<string, DataObjects.SignalRConnectionInfo> _connections = new();
        
        private List<string> tenants = new List<string>();

        /// <summary>
        /// Gets all currently active SignalR connections.
        /// </summary>
        public static IReadOnlyDictionary<string, DataObjects.SignalRConnectionInfo> ActiveConnections => _connections;

        /// <summary>
        /// Gets a snapshot of all active connections as a list.
        /// </summary>
        public static List<DataObjects.SignalRConnectionInfo> GetActiveConnectionsList()
        {
            return _connections.Values.ToList();
        }

        /// <summary>
        /// Gets connection info by connection ID.
        /// </summary>
        public static DataObjects.SignalRConnectionInfo? GetConnectionInfo(string connectionId)
        {
            _connections.TryGetValue(connectionId, out var connectionInfo);
            return connectionInfo;
        }

        /// <summary>
        /// Checks if a connection ID exists.
        /// </summary>
        public static bool ConnectionExists(string connectionId)
        {
            return _connections.ContainsKey(connectionId);
        }

        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            var userId = Context.User?.Identity?.Name ?? "Anonymous";
            var userIdentifier = Context.UserIdentifier;
            
            // Try to get additional info from the HTTP context
            var httpContext = Context.GetHttpContext();
            string? ipAddress = null;
            string? userAgent = null;
            string? transportType = null;
            
            if (httpContext != null) {
                // Get IP address
                ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                
                // Check for forwarded headers (load balancer / proxy)
                if (httpContext.Request.Headers.ContainsKey("X-Forwarded-For")) {
                    ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim() ?? ipAddress;
                }
                
                // Get user agent
                userAgent = httpContext.Request.Headers["User-Agent"].FirstOrDefault();
                
                // Get transport type from query string
                transportType = httpContext.Request.Query["transport"].FirstOrDefault();
            }
            
            var connectionInfo = new DataObjects.SignalRConnectionInfo {
                ConnectionId = connectionId,
                UserId = userId,
                UserIdentifier = userIdentifier,
                HubName = "freecicdHub",
                ConnectedAt = DateTime.UtcNow,
                Groups = new List<string>(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                TransportType = transportType
            };

            _connections.TryAdd(connectionId, connectionInfo);
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            _connections.TryRemove(connectionId, out _);
            
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinTenantId(string TenantId)
        {
            if (!tenants.Contains(TenantId)) {
                tenants.Add(TenantId);
            }

            // Before adding a user to a Tenant group remove them from any groups they were in before.
            if (tenants != null && tenants.Count() > 0) {
                foreach (var tenant in tenants) {
                    try {
                        await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenant);
                    } catch { }
                }
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, TenantId);
            
            // Update connection info with group membership
            if (_connections.TryGetValue(Context.ConnectionId, out var connectionInfo)) {
                if (!connectionInfo.Groups.Contains(TenantId)) {
                    connectionInfo.Groups.Add(TenantId);
                }
                connectionInfo.LastActivityAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Registers the browser fingerprint for this connection.
        /// This allows linking multiple connections from the same browser.
        /// </summary>
        public Task RegisterFingerprint(string fingerprint)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var connectionInfo)) {
                connectionInfo.Fingerprint = fingerprint;
                connectionInfo.LastActivityAt = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Updates the client state (current page, focus, visibility, etc.).
        /// Called periodically by clients or when state changes.
        /// </summary>
        public Task UpdateClientState(DataObjects.SignalRClientState state)
        {
            if (_connections.TryGetValue(Context.ConnectionId, out var connectionInfo)) {
                connectionInfo.CurrentPage = state.CurrentPage;
                connectionInfo.HasFocus = state.HasFocus;
                connectionInfo.IsVisible = state.IsVisible;
                connectionInfo.ScreenWidth = state.ScreenWidth;
                connectionInfo.ScreenHeight = state.ScreenHeight;
                connectionInfo.Timezone = state.Timezone;
                connectionInfo.Language = state.Language;
                connectionInfo.LastStateUpdate = DateTime.UtcNow;
                connectionInfo.LastActivityAt = DateTime.UtcNow;
                
                // Parse user agent if not already done
                if (connectionInfo.DeviceType == null && !string.IsNullOrWhiteSpace(connectionInfo.UserAgent)) {
                    ParseUserAgent(connectionInfo);
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Parses the user agent string to extract device type and browser name.
        /// </summary>
        private static void ParseUserAgent(DataObjects.SignalRConnectionInfo connectionInfo)
        {
            var ua = connectionInfo.UserAgent?.ToLower() ?? "";
            
            // Detect device type
            if (ua.Contains("mobile") || ua.Contains("android") || ua.Contains("iphone") || ua.Contains("ipad")) {
                connectionInfo.DeviceType = ua.Contains("tablet") || ua.Contains("ipad") ? "Tablet" : "Mobile";
            } else {
                connectionInfo.DeviceType = "Desktop";
            }
            
            // Detect browser
            if (ua.Contains("edg/")) {
                connectionInfo.BrowserName = "Edge";
            } else if (ua.Contains("chrome/") && !ua.Contains("edg/")) {
                connectionInfo.BrowserName = "Chrome";
            } else if (ua.Contains("firefox/")) {
                connectionInfo.BrowserName = "Firefox";
            } else if (ua.Contains("safari/") && !ua.Contains("chrome/")) {
                connectionInfo.BrowserName = "Safari";
            } else if (ua.Contains("opera") || ua.Contains("opr/")) {
                connectionInfo.BrowserName = "Opera";
            } else {
                connectionInfo.BrowserName = "Unknown";
            }
        }

        public async Task SignalRUpdate(DataObjects.SignalRUpdate update)
        {
            // Track activity
            if (_connections.TryGetValue(Context.ConnectionId, out var connectionInfo)) {
                connectionInfo.LastActivityAt = DateTime.UtcNow;
                connectionInfo.MessageCount++;
            }

            if (update.TenantId.HasValue) {
                await Clients.Group(update.TenantId.Value.ToString()).SignalRUpdate(update);
            } else {
                // This is a non-tenant-specific update.
                await Clients.All.SignalRUpdate(update);
            }
        }

        /// <summary>
        /// Joins the live pipeline monitoring group.
        /// Clients in this group receive real-time status updates from the background polling service.
        /// </summary>
        public async Task JoinPipelineMonitor()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DataObjects.SignalRUpdateType.PipelineMonitorGroup);

            if (_connections.TryGetValue(Context.ConnectionId, out var connectionInfo)) {
                if (!connectionInfo.Groups.Contains(DataObjects.SignalRUpdateType.PipelineMonitorGroup)) {
                    connectionInfo.Groups.Add(DataObjects.SignalRUpdateType.PipelineMonitorGroup);
                }
                connectionInfo.LastActivityAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Leaves the live pipeline monitoring group.
        /// </summary>
        public async Task LeavePipelineMonitor()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, DataObjects.SignalRUpdateType.PipelineMonitorGroup);

            if (_connections.TryGetValue(Context.ConnectionId, out var connectionInfo)) {
                connectionInfo.Groups.Remove(DataObjects.SignalRUpdateType.PipelineMonitorGroup);
                connectionInfo.LastActivityAt = DateTime.UtcNow;
            }
        }
    }
}

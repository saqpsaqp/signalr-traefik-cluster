using StackExchange.Redis;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Background service that automatically registers this node in Redis for service discovery
/// Maintains a heartbeat and provides automatic cleanup when nodes go offline
/// </summary>
public class NodeRegistrationService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<NodeRegistrationService> _logger;
    private readonly string _nodeId;
    private readonly string _ipAddress;

    public NodeRegistrationService(IConnectionMultiplexer redis, ILogger<NodeRegistrationService> logger)
    {
        _redis = redis;
        _logger = logger;
        _nodeId = Environment.MachineName;
        _ipAddress = GetLocalIPAddress();
    }

    /// <summary>
    /// Main execution loop that maintains node registration in Redis with heartbeat
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();
        var key = $"signalr:nodes:{_nodeId}";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Register/update this node in Redis with current timestamp
                await db.HashSetAsync(key, new HashEntry[]
                {
                    new("nodeId", _nodeId),
                    new("name", $"Node {_nodeId}"),
                    new("url", "/chathub"),
                    new("description", $"SignalR Node {_nodeId} running on {_ipAddress}"),
                    new("ipAddress", _ipAddress),
                    new("lastSeen", DateTime.UtcNow.ToString("O")),
                    new("status", "healthy"),
                    new("type", "node")
                });

                // Set TTL for automatic cleanup if node becomes unresponsive
                await db.KeyExpireAsync(key, TimeSpan.FromSeconds(45));

                _logger.LogInformation("Node {NodeId} registered at {IpAddress}", _nodeId, _ipAddress);

                // Heartbeat interval - update registration every 15 seconds
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering node {NodeId}", _nodeId);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        // Cleanup on graceful shutdown
        try
        {
            await db.KeyDeleteAsync(key);
            _logger.LogInformation("Node {NodeId} unregistered", _nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering node {NodeId}", _nodeId);
        }
    }

    /// <summary>
    /// Gets the local IP address of this node for service discovery
    /// </summary>
    /// <returns>IP address string or "unknown" if unable to determine</returns>
    private static string GetLocalIPAddress()
    {
        try
        {
            // Use a dummy connection to determine the local IP address
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
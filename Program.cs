using SignalRChat.Hubs;
using StackExchange.Redis;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Configure SignalR with Redis backplane for message distribution across nodes
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

// Add Redis connection for service discovery and node registration
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(connectionString);
});

// Register background service for automatic node registration in Redis
builder.Services.AddHostedService<NodeRegistrationService>();

// Configure CORS for web clients
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:8080", "http://localhost:8888", "http://localhost:3000")
              .SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();

// Map SignalR hub endpoint
app.MapHub<ChatHub>("/chathub");

// Basic endpoint to verify service is running
app.MapGet("/", () => "SignalR Chat Hub is running!");

// Health check endpoint for load balancer monitoring
app.MapGet("/health", () => Results.Ok(new { status = "healthy", node = Environment.MachineName }));

// API endpoint to get active nodes from Redis service discovery
app.MapGet("/nodes", async (IConnectionMultiplexer redis) => 
{
    var db = redis.GetDatabase();
    var server = redis.GetServer(redis.GetEndPoints().First());
    
    // Get active nodes from Redis service registry
    var nodeKeys = server.Keys(pattern: "signalr:nodes:*").ToList();
    var activeNodes = new List<object>();
    
    foreach (var key in nodeKeys)
    {
        var nodeData = await db.HashGetAllAsync(key);
        if (nodeData.Any())
        {
            var nodeInfo = nodeData.ToDictionary(x => x.Name, x => x.Value);
            if (nodeInfo.ContainsKey("lastSeen"))
            {
                var lastSeen = DateTime.Parse(nodeInfo["lastSeen"]);
                // Only include nodes that have been seen in the last 30 seconds (health check)
                if (DateTime.UtcNow - lastSeen < TimeSpan.FromSeconds(30))
                {
                    activeNodes.Add(new 
                    { 
                        id = nodeInfo["nodeId"].ToString(),
                        name = nodeInfo["name"].ToString(),
                        url = nodeInfo["url"].ToString(),
                        description = nodeInfo["description"].ToString(),
                        lastSeen = lastSeen,
                        ipAddress = nodeInfo["ipAddress"].ToString()
                    });
                }
            }
        }
    }
    
    // Add load balanced option as default
    if (activeNodes.Any())
    {
        activeNodes.Insert(0, new 
        { 
            id = "balanced", 
            name = "Load Balanced", 
            url = "/chathub", 
            description = $"Auto-balanced across {activeNodes.Count} active nodes",
            lastSeen = DateTime.UtcNow,
            ipAddress = "load-balancer"
        });
    }
    
    return Results.Ok(new { 
        available_nodes = activeNodes,
        current_node = Environment.MachineName,
        active_count = activeNodes.Count - 1, // Exclude load balancer from count
        timestamp = DateTime.UtcNow
    });
});

app.Run();
using Microsoft.AspNetCore.SignalR;

namespace SignalRChat.Hubs;

/// <summary>
/// SignalR Hub for real-time chat functionality with multi-node support
/// </summary>
public class ChatHub : Hub
{
    private readonly string _nodeId = Environment.MachineName;

    /// <summary>
    /// Sends a message to all connected clients across all nodes
    /// </summary>
    /// <param name="user">Username sending the message</param>
    /// <param name="message">Message content</param>
    public async Task SendMessage(string user, string message)
    {
        // Broadcast to all clients with node identification for debugging
        await Clients.All.SendAsync("ReceiveMessage", user, message, Context.ConnectionId, _nodeId);
    }

    /// <summary>
    /// Returns information about the current node handling this connection
    /// </summary>
    public async Task GetNodeInfo()
    {
        await Clients.Caller.SendAsync("NodeInfo", _nodeId, Context.ConnectionId);
    }

    /// <summary>
    /// Adds the current connection to a group (chat room)
    /// </summary>
    /// <param name="groupName">Name of the group to join</param>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("ReceiveMessage", "System", $"{Context.ConnectionId} joined {groupName}", Context.ConnectionId, _nodeId);
    }

    /// <summary>
    /// Removes the current connection from a group (chat room)
    /// </summary>
    /// <param name="groupName">Name of the group to leave</param>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("ReceiveMessage", "System", $"{Context.ConnectionId} left {groupName}", Context.ConnectionId, _nodeId);
    }

    /// <summary>
    /// Sends a message to all clients in a specific group
    /// </summary>
    /// <param name="groupName">Target group name</param>
    /// <param name="user">Username sending the message</param>
    /// <param name="message">Message content</param>
    public async Task SendToGroup(string groupName, string user, string message)
    {
        await Clients.Group(groupName).SendAsync("ReceiveMessage", user, message, Context.ConnectionId, _nodeId);
    }

    /// <summary>
    /// Called when a client connects to this hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await Clients.All.SendAsync("ReceiveMessage", "System", $"{Context.ConnectionId} connected to {_nodeId}", Context.ConnectionId, _nodeId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from this hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.All.SendAsync("ReceiveMessage", "System", $"{Context.ConnectionId} disconnected from {_nodeId}", Context.ConnectionId, _nodeId);
        await base.OnDisconnectedAsync(exception);
    }
}
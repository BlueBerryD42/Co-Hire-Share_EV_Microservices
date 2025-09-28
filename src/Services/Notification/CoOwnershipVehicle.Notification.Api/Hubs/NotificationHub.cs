using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace CoOwnershipVehicle.Notification.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            // Join user-specific group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            
            // Join group-specific groups if user is part of any groups
            var groupClaims = Context.User?.FindAll("group")?.Select(c => c.Value);
            if (groupClaims != null)
            {
                foreach (var groupId in groupClaims)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");
                }
            }
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            
            var groupClaims = Context.User?.FindAll("group")?.Select(c => c.Value);
            if (groupClaims != null)
            {
                foreach (var groupId in groupClaims)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");
                }
            }
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    // Client can call this to join a specific group
    public async Task JoinGroup(string groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");
    }

    // Client can call this to leave a specific group
    public async Task LeaveGroup(string groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");
    }
}

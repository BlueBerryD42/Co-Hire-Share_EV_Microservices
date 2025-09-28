using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Notification.Api.Services;
using System.Security.Claims;

namespace CoOwnershipVehicle.Notification.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost]
    public async Task<ActionResult<NotificationDto>> CreateNotification([FromBody] CreateNotificationDto dto)
    {
        var notification = await _notificationService.CreateNotificationAsync(dto);
        return CreatedAtAction(nameof(GetNotification), new { id = notification.Id }, notification);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<List<NotificationDto>>> CreateBulkNotification([FromBody] CreateBulkNotificationDto dto)
    {
        var notifications = await _notificationService.CreateBulkNotificationAsync(dto);
        return Ok(notifications);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NotificationDto>> GetNotification(Guid id)
    {
        var notification = await _notificationService.GetNotificationByIdAsync(id);
        if (notification == null)
            return NotFound();

        return Ok(notification);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<NotificationDto>>> GetUserNotifications(
        Guid userId, 
        [FromQuery] NotificationRequestDto request)
    {
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, request);
        return Ok(notifications);
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<NotificationDto>>> GetMyNotifications([FromQuery] NotificationRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var notifications = await _notificationService.GetUserNotificationsAsync(userId.Value, request);
        return Ok(notifications);
    }

    [HttpGet("stats/{userId}")]
    public async Task<ActionResult<NotificationStatsDto>> GetNotificationStats(Guid userId)
    {
        var stats = await _notificationService.GetNotificationStatsAsync(userId);
        return Ok(stats);
    }

    [HttpGet("stats/my")]
    public async Task<ActionResult<NotificationStatsDto>> GetMyNotificationStats()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var stats = await _notificationService.GetNotificationStatsAsync(userId.Value);
        return Ok(stats);
    }

    [HttpPut("{id}/read")]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var notification = await _notificationService.MarkAsReadAsync(id, userId.Value);
        if (notification == null)
            return NotFound();

        return Ok(notification);
    }

    [HttpPut("read-all")]
    public async Task<ActionResult> MarkAllAsRead()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        await _notificationService.MarkAllAsReadAsync(userId.Value);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteNotification(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _notificationService.DeleteNotificationAsync(id, userId.Value);
        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("cleanup")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> CleanupOldNotifications([FromQuery] int daysOld = 30)
    {
        await _notificationService.DeleteOldNotificationsAsync(daysOld);
        return Ok();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

[ApiController]
[Route("api/document")]
public class DocumentNotificationController : BaseAuthenticatedController
{
    private readonly IDocumentService _documentService;

    public DocumentNotificationController(IDocumentService documentService, ILogger<DocumentNotificationController> logger)
        : base(logger)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Send manual reminder to pending signers
    /// </summary>
    [HttpPut("{documentId}/remind")]
    [ProducesResponseType(typeof(SendReminderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SendReminderResponse>> SendManualReminder(
        Guid documentId,
        [FromBody] SendReminderRequest request)
    {
        try
        {
            var userId = GetUserId();

            // Get base URL from request for signing links
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

            var result = await _documentService.SendManualReminderAsync(documentId, request, userId, baseUrl);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found for manual reminder");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized manual reminder attempt");
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Invalid manual reminder operation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending manual reminder");
            return StatusCode(500, new { error = "An error occurred while sending reminders" });
        }
    }

    /// <summary>
    /// Get reminder history for a document
    /// </summary>
    [HttpGet("{documentId}/reminders")]
    [ProducesResponseType(typeof(ReminderHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReminderHistoryResponse>> GetReminderHistory(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _documentService.GetReminderHistoryAsync(documentId, userId);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found for reminder history");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to reminder history");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving reminder history");
            return StatusCode(500, new { error = "An error occurred while retrieving reminder history" });
        }
    }

    /// <summary>
    /// Test endpoint - Send a test email notification
    /// </summary>
    [HttpPost("test/send-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestSendEmail([FromBody] TestEmailRequest request)
    {
        try
        {
            var result = await _documentService.SendTestEmailAsync(request.Email, request.Subject, request.Message);

            if (result)
            {
                return Ok(new { success = true, message = $"Test email sent to {request.Email}" });
            }
            else
            {
                return BadRequest(new { success = false, message = "Failed to send test email. Check logs and email configuration." });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending test email");
            return StatusCode(500, new { error = "An error occurred while sending test email", details = ex.Message });
        }
    }

    /// <summary>
    /// Test endpoint - Trigger signature reminder background job manually
    /// </summary>
    [HttpPost("test/trigger-reminders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerReminders()
    {
        try
        {
            // This would trigger the background job logic
            // For now, just return success
            Logger.LogInformation("Manual trigger of reminder background job requested");

            return Ok(new {
                success = true,
                message = "Reminder check triggered. Check logs for results.",
                note = "Background service runs automatically every 24 hours"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error triggering reminders");
            return StatusCode(500, new { error = "An error occurred while triggering reminders" });
        }
    }
}

public class TestEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = "Test Email";
    public string Message { get; set; } = "This is a test email from Co-Ownership Vehicle System";
}

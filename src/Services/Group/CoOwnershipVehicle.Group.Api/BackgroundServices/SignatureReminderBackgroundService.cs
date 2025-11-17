using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoOwnershipVehicle.Group.Api.BackgroundServices;

public class SignatureReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SignatureReminderBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _runInterval;

    public SignatureReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SignatureReminderBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;

        // Default to run daily at 9 AM, configurable via appsettings
        var intervalHours = configuration.GetValue<int>("SignatureReminders:IntervalHours", 24);
        _runInterval = TimeSpan.FromHours(intervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Signature Reminder Background Service started. Running every {Hours} hours.", _runInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is being stopped, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                // Only log if we're not shutting down
                if (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogError(ex, "Error occurred while processing signature reminders");
                    }
                    catch
                    {
                        // Ignore logging failures during shutdown
                    }
                }
            }

            // Wait for the next run
            try
            {
                await Task.Delay(_runInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is being stopped
                break;
            }
        }

        _logger.LogInformation("Signature Reminder Background Service stopped");
    }

    private async Task ProcessPendingRemindersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting signature reminder processing run at {Time}", DateTime.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var threeDaysFromNow = now.AddDays(3);
        var oneDayFromNow = now.AddDays(1);

        // Find all pending signatures with due dates
        var pendingSignatures = await context.DocumentSignatures
            .Include(s => s.Document)
            .Where(s => s.Status == SignatureStatus.SentForSigning &&
                       s.DueDate.HasValue &&
                       s.DueDate.Value > now) // Not yet expired
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} pending signatures to process", pendingSignatures.Count);

        var remindersSent = 0;
        var remindersFailed = 0;

        foreach (var signature in pendingSignatures)
        {
            try
            {
                var dueDate = signature.DueDate!.Value;
                ReminderType? reminderType = null;

                // Determine if a reminder should be sent
                if (dueDate <= threeDaysFromNow && dueDate > now.AddDays(2.5))
                {
                    // 3 days before reminder
                    if (!await HasReminderBeenSent(context, signature.Id, ReminderType.ThreeDaysBefore, cancellationToken))
                    {
                        reminderType = ReminderType.ThreeDaysBefore;
                    }
                }
                else if (dueDate <= oneDayFromNow && dueDate > now.AddHours(12))
                {
                    // 1 day before reminder
                    if (!await HasReminderBeenSent(context, signature.Id, ReminderType.OneDayBefore, cancellationToken))
                    {
                        reminderType = ReminderType.OneDayBefore;
                    }
                }

                if (reminderType.HasValue)
                {
                    await SendReminderAsync(
                        context,
                        notificationService,
                        signature,
                        reminderType.Value,
                        cancellationToken);

                    remindersSent++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process reminder for signature {SignatureId}", signature.Id);
                remindersFailed++;
            }
        }

        // Process overdue signatures
        await ProcessOverdueSignaturesAsync(context, notificationService, cancellationToken);

        // Process expired signatures
        await ProcessExpiredSignaturesAsync(context, notificationService, cancellationToken);

        _logger.LogInformation(
            "Reminder processing completed. Sent: {Sent}, Failed: {Failed}",
            remindersSent, remindersFailed);
    }

    private async Task ProcessOverdueSignaturesAsync(
        GroupDbContext context,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Find signatures that are overdue but not yet marked as expired
        var overdueSignatures = await context.DocumentSignatures
            .Include(s => s.Document)
            .Where(s => s.Status == SignatureStatus.SentForSigning &&
                       s.DueDate.HasValue &&
                       s.DueDate.Value < now &&
                       s.DueDate.Value > now.AddDays(-1)) // Overdue within last 24 hours
            .ToListAsync(cancellationToken);

        foreach (var signature in overdueSignatures)
        {
            try
            {
                if (!await HasReminderBeenSent(context, signature.Id, ReminderType.Overdue, cancellationToken))
                {
                    await SendReminderAsync(
                        context,
                        notificationService,
                        signature,
                        ReminderType.Overdue,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send overdue reminder for signature {SignatureId}", signature.Id);
            }
        }
    }

    private async Task ProcessExpiredSignaturesAsync(
        GroupDbContext context,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Find signatures that have expired
        var expiredSignatures = await context.DocumentSignatures
            .Include(s => s.Document)
            .Where(s => s.Status == SignatureStatus.SentForSigning &&
                       s.DueDate.HasValue &&
                       s.DueDate.Value < now)
            .ToListAsync(cancellationToken);

        // Get user service client for fetching user data
        using var scope = _serviceProvider.CreateScope();
        var userServiceClient = scope.ServiceProvider.GetRequiredService<IUserServiceClient>();
        // For background services, we'll use an empty token or service token
        // Note: This assumes the User Service endpoint allows internal calls
        // In production, use a service account token
        var accessToken = _configuration["ServiceTokens:Internal"] ?? string.Empty;

        foreach (var signature in expiredSignatures)
        {
            try
            {
                // Mark as expired
                signature.Status = SignatureStatus.Expired;

                // Get document owner and signer via HTTP
                var document = signature.Document;
                var userIds = new List<Guid> { signature.SignerId };
                if (document.UploadedBy.HasValue)
                {
                    userIds.Add(document.UploadedBy.Value);
                }

                var users = await userServiceClient.GetUsersAsync(userIds, accessToken);
                var signer = users.GetValueOrDefault(signature.SignerId);
                var documentOwner = document.UploadedBy.HasValue 
                    ? users.GetValueOrDefault(document.UploadedBy.Value) 
                    : null;

                if (signer != null && documentOwner != null)
                {
                    await notificationService.SendSignatureExpiredNotificationAsync(
                        signer,
                        documentOwner,
                        document);
                }

                await context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Signature {SignatureId} marked as expired and notifications sent",
                    signature.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process expired signature {SignatureId}", signature.Id);
            }
        }
    }

    private async Task<bool> HasReminderBeenSent(
        GroupDbContext context,
        Guid signatureId,
        ReminderType reminderType,
        CancellationToken cancellationToken)
    {
        return await context.SignatureReminders
            .AnyAsync(r => r.DocumentSignatureId == signatureId &&
                          r.ReminderType == reminderType,
                     cancellationToken);
    }

    private async Task SendReminderAsync(
        GroupDbContext context,
        INotificationService notificationService,
        DocumentSignature signature,
        ReminderType reminderType,
        CancellationToken cancellationToken)
    {
        // Get base URL from configuration, fallback to default Group Service port
        var baseUrl = _configuration["SignatureReminders:BaseUrl"] 
            ?? "https://localhost:61603"; // Default to Group Service port from launchSettings.json
        var signingUrl = $"{baseUrl}/api/document/{signature.DocumentId}/sign?token={signature.SigningToken}";

        // Get signer user data via HTTP
        using var scope = _serviceProvider.CreateScope();
        var userServiceClient = scope.ServiceProvider.GetRequiredService<IUserServiceClient>();
        var accessToken = _configuration["ServiceTokens:Internal"] ?? string.Empty;
        var signer = await userServiceClient.GetUserAsync(signature.SignerId, accessToken);

        if (signer == null)
        {
            _logger.LogWarning("Signer {SignerId} not found for signature {SignatureId}", signature.SignerId, signature.Id);
            return;
        }

        var success = await notificationService.SendSignatureReminderAsync(
            signer,
            signature.Document,
            signingUrl,
            reminderType);

        // Record the reminder
        var reminder = new SignatureReminder
        {
            Id = Guid.NewGuid(),
            DocumentSignatureId = signature.Id,
            ReminderType = reminderType,
            SentAt = DateTime.UtcNow,
            SentBy = Guid.Empty, // System-generated
            IsManual = false,
            Status = success ? ReminderDeliveryStatus.Sent : ReminderDeliveryStatus.Failed,
            DeliveredAt = success ? DateTime.UtcNow : null,
            ErrorMessage = success ? null : "Failed to send email",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.SignatureReminders.Add(reminder);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Sent {ReminderType} reminder for signature {SignatureId}. Success: {Success}",
            reminderType, signature.Id, success);
    }
}

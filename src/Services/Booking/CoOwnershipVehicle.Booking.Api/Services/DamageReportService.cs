using System.Text.Json;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Services;

public interface IDamageReportService
{
    Task<DamageReportDto> ReportDamageAsync(Guid checkInId, Guid userId, CreateDamageReportDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DamageReportDto>> GetByCheckInAsync(Guid checkInId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DamageReportDto>> GetByBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DamageReportDto>> GetByVehicleAsync(Guid vehicleId, Guid userId, CancellationToken cancellationToken = default);
    Task<DamageReportDto> UpdateStatusAsync(Guid reportId, Guid userId, UpdateDamageReportStatusDto request, CancellationToken cancellationToken = default);
}

public class DamageReportService : IDamageReportService
{
    private readonly BookingDbContext _context;
    private readonly ILogger<DamageReportService> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    public DamageReportService(BookingDbContext context, ILogger<DamageReportService> logger, IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<DamageReportDto> ReportDamageAsync(Guid checkInId, Guid userId, CreateDamageReportDto request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentException("Damage report payload is required.");
        }

        var checkIn = await _context.CheckIns
            .Include(ci => ci.Booking)
                .ThenInclude(b => b.Group)
                    .ThenInclude(g => g.Members)
            .Include(ci => ci.Booking)
                .ThenInclude(b => b.Vehicle)
            .Include(ci => ci.Photos)
            .FirstOrDefaultAsync(ci => ci.Id == checkInId, cancellationToken);

        if (checkIn == null)
        {
            throw new KeyNotFoundException("Check-in not found for damage reporting.");
        }

        var booking = checkIn.Booking ?? throw new InvalidOperationException("Check-in is missing its booking relationship.");

        if (checkIn.UserId != userId && !await UserHasAccessAsync(userId, booking.GroupId, cancellationToken))
        {
            throw new UnauthorizedAccessException("You do not have access to this check-in record.");
        }

        var estimatedCost = request.EstimatedCost ?? EstimateDamageCost(request.Severity);

        var normalizedPhotoIds = (request.PhotoIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (checkIn.Photos != null && normalizedPhotoIds.Count > 0)
        {
            foreach (var photo in checkIn.Photos.Where(p => normalizedPhotoIds.Contains(p.Id)))
            {
                photo.Type = PhotoType.Damage;
            }
        }

        booking.RequiresDamageReview = true;

        var report = new DamageReport
        {
            Id = Guid.NewGuid(),
            CheckInId = checkIn.Id,
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            GroupId = booking.GroupId,
            ReportedByUserId = userId,
            Description = request.Description.Trim(),
            Severity = request.Severity,
            Location = request.Location,
            EstimatedCost = estimatedCost,
            Status = DamageReportStatus.Reported,
            PhotoIdsJson = normalizedPhotoIds.Count > 0 ? JsonSerializer.Serialize(normalizedPhotoIds) : null
        };

        _context.DamageReports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);

        await PublishDamageReportedEvent(report, normalizedPhotoIds, cancellationToken);

        if (request.Severity == DamageSeverity.Severe)
        {
            await NotifyGroupAdminsAsync(booking, report, cancellationToken);
        }

        _logger.LogInformation("Damage report {ReportId} created for check-in {CheckInId}", report.Id, checkInId);

        return MapToDto(report);
    }

    public async Task<IReadOnlyList<DamageReportDto>> GetByCheckInAsync(Guid checkInId, Guid userId, CancellationToken cancellationToken = default)
    {
        var checkIn = await _context.CheckIns
            .Include(ci => ci.Booking)
            .FirstOrDefaultAsync(ci => ci.Id == checkInId, cancellationToken);

        if (checkIn == null)
        {
            throw new KeyNotFoundException("Check-in not found.");
        }

        if (checkIn.UserId != userId && !await UserHasAccessAsync(userId, checkIn.Booking.GroupId, cancellationToken))
        {
            throw new UnauthorizedAccessException("You do not have access to this check-in.");
        }

        var reports = await _context.DamageReports
            .AsNoTracking()
            .Where(r => r.CheckInId == checkInId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return reports.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<DamageReportDto>> GetByBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .Include(b => b.Group)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking == null)
        {
            throw new KeyNotFoundException("Booking not found.");
        }

        if (booking.UserId != userId && !await UserHasAccessAsync(userId, booking.GroupId, cancellationToken))
        {
            throw new UnauthorizedAccessException("You do not have access to this booking.");
        }

        var reports = await _context.DamageReports
            .AsNoTracking()
            .Where(r => r.BookingId == bookingId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return reports.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<DamageReportDto>> GetByVehicleAsync(Guid vehicleId, Guid userId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await _context.Vehicles
            .AsNoTracking()
            .AnyAsync(v => v.Id == vehicleId && v.Group != null && v.Group.Members.Any(m => m.UserId == userId), cancellationToken);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this vehicle.");
        }

        var reports = await _context.DamageReports
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return reports.Select(MapToDto).ToList();
    }

    public async Task<DamageReportDto> UpdateStatusAsync(Guid reportId, Guid userId, UpdateDamageReportStatusDto request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentException("Status update payload is required.");
        }

        var report = await _context.DamageReports
            .Include(r => r.CheckIn)
                .ThenInclude(ci => ci.Booking)
                    .ThenInclude(b => b.Group)
                        .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report == null)
        {
            throw new KeyNotFoundException("Damage report not found.");
        }

        var booking = report.CheckIn.Booking ?? throw new InvalidOperationException("Damage report is missing booking information.");

        if (!await IsGroupAdminAsync(userId, booking.GroupId, cancellationToken))
        {
            throw new UnauthorizedAccessException("Only group administrators can update damage report status.");
        }

        report.Status = request.Status;
        if (request.EstimatedCost.HasValue)
        {
            report.EstimatedCost = request.EstimatedCost.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            report.Notes = request.Notes.Trim();
        }

        if (request.ExpenseId.HasValue && request.ExpenseId != Guid.Empty)
        {
            report.ExpenseId = request.ExpenseId;
        }

        if (request.Status == DamageReportStatus.Resolved)
        {
            report.ResolvedAt = DateTime.UtcNow;
            report.ResolvedByUserId = userId;
            booking.RequiresDamageReview = false;
        }
        else
        {
            booking.RequiresDamageReview = true;
            report.ResolvedAt = null;
            report.ResolvedByUserId = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new DamageReportStatusChangedEvent
        {
            DamageReportId = report.Id,
            BookingId = report.BookingId,
            VehicleId = report.VehicleId,
            GroupId = report.GroupId,
            Status = report.Status,
            ChangedByUserId = userId,
            EstimatedCost = report.EstimatedCost,
            ExpenseId = report.ExpenseId,
            Notes = report.Notes,
            ChangedAt = DateTime.UtcNow
        }, cancellationToken);

        _logger.LogInformation("Damage report {ReportId} status updated to {Status}", report.Id, report.Status);

        return MapToDto(report);
    }

    private async Task NotifyGroupAdminsAsync(CoOwnershipVehicle.Domain.Entities.Booking booking, DamageReport report, CancellationToken cancellationToken)
    {
        var adminUserIds = booking.Group.Members
            .Where(m => m.RoleInGroup == GroupRole.Admin)
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        if (!adminUserIds.Contains(booking.UserId))
        {
            adminUserIds.Add(booking.UserId);
        }

        if (!adminUserIds.Any())
        {
            return;
        }

        var vehicle = await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == booking.VehicleId, cancellationToken);
        var message = $"Severe vehicle damage reported for {vehicle?.Model ?? "vehicle"} ({vehicle?.PlateNumber}). Severity: {report.Severity}. Location: {report.Location}.";

        await _publishEndpoint.Publish(new BulkNotificationEvent
        {
            UserIds = adminUserIds,
            GroupId = booking.GroupId,
            Title = "Severe vehicle damage reported",
            Message = message,
            Type = "DamageReported",
            Priority = "High",
            ActionUrl = $"/bookings/{booking.Id}",
            ActionText = "Review damage report"
        }, cancellationToken);
    }

    private async Task PublishDamageReportedEvent(DamageReport report, List<Guid> photoIds, CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish(new DamageReportedEvent
        {
            DamageReportId = report.Id,
            CheckInId = report.CheckInId,
            BookingId = report.BookingId,
            VehicleId = report.VehicleId,
            GroupId = report.GroupId,
            ReportedByUserId = report.ReportedByUserId,
            Severity = report.Severity,
            Location = report.Location,
            EstimatedCost = report.EstimatedCost,
            Description = report.Description,
            PhotoIds = photoIds
        }, cancellationToken);
    }

    private static decimal EstimateDamageCost(DamageSeverity severity)
    {
        return severity switch
        {
            DamageSeverity.Minor => 250m,
            DamageSeverity.Moderate => 750m,
            DamageSeverity.Severe => 2500m,
            _ => 250m
        };
    }

    private async Task<bool> UserHasAccessAsync(Guid userId, Guid groupId, CancellationToken cancellationToken)
    {
        return await _context.GroupMembers
            .AsNoTracking()
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, cancellationToken);
    }

    private async Task<bool> IsGroupAdminAsync(Guid userId, Guid groupId, CancellationToken cancellationToken)
    {
        return await _context.GroupMembers
            .AsNoTracking()
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId &&
                            (gm.RoleInGroup == GroupRole.Admin), cancellationToken);
    }

    private static DamageReportDto MapToDto(DamageReport report)
    {
        List<Guid> photoIds = new();
        if (!string.IsNullOrWhiteSpace(report.PhotoIdsJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<Guid>>(report.PhotoIdsJson);
                if (parsed != null)
                {
                    photoIds = parsed;
                }
            }
            catch (JsonException)
            {
                // ignore malformed json
            }
        }

        return new DamageReportDto
        {
            Id = report.Id,
            CheckInId = report.CheckInId,
            BookingId = report.BookingId,
            VehicleId = report.VehicleId,
            GroupId = report.GroupId,
            ReportedByUserId = report.ReportedByUserId,
            Description = report.Description,
            Severity = report.Severity,
            Location = report.Location,
            EstimatedCost = report.EstimatedCost,
            Status = report.Status,
            Notes = report.Notes,
            ExpenseId = report.ExpenseId,
            ResolvedByUserId = report.ResolvedByUserId,
            ResolvedAt = report.ResolvedAt,
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt,
            PhotoIds = photoIds
        };
    }
}

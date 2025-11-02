using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class BookingTemplateService : IBookingTemplateService
{
    private readonly IBookingTemplateRepository _bookingTemplateRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<BookingTemplateService> _logger;

    public BookingTemplateService(
        IBookingTemplateRepository bookingTemplateRepository,
        IBookingRepository bookingRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<BookingTemplateService> logger)
    {
        _bookingTemplateRepository = bookingTemplateRepository ?? throw new ArgumentNullException(nameof(bookingTemplateRepository));
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BookingTemplateResponse> CreateBookingTemplateAsync(CreateBookingTemplateRequest request, Guid userId)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.Duration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Duration must be greater than zero.", nameof(request.Duration));
        }

        if (request.PreferredStartTime < TimeSpan.Zero || request.PreferredStartTime >= TimeSpan.FromDays(1))
        {
            throw new ArgumentException("PreferredStartTime must fall within a 24-hour range.", nameof(request.PreferredStartTime));
        }

        if (request.VehicleId.HasValue)
        {
            var hasAccess = await _bookingRepository.UserHasVehicleAccessAsync(request.VehicleId.Value, userId);
            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("You do not have access to the specified vehicle.");
            }
        }

        var template = new BookingTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            VehicleId = request.VehicleId,
            Duration = request.Duration,
            PreferredStartTime = request.PreferredStartTime,
            Purpose = request.Purpose,
            Notes = request.Notes,
            Priority = request.Priority,
            UsageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _bookingTemplateRepository.AddAsync(template);
        await _bookingTemplateRepository.SaveChangesAsync();

        _logger.LogInformation("Created booking template {TemplateId} for user {UserId}.", template.Id, userId);

        return MapToDto(template);
    }

    public async Task<IReadOnlyList<BookingTemplateResponse>> GetUserBookingTemplatesAsync(Guid userId)
    {
        var templates = await _bookingTemplateRepository.GetByUserAsync(userId);
        return templates.Select(MapToDto).ToList();
    }

    public async Task<BookingTemplateResponse?> GetBookingTemplateByIdAsync(Guid templateId, Guid userId)
    {
        var template = await _bookingTemplateRepository.GetByIdAsync(templateId);
        if (template == null || template.UserId != userId)
        {
            return null;
        }
        return MapToDto(template);
    }

    public async Task<BookingDto> CreateBookingFromTemplateAsync(Guid templateId, CreateBookingFromTemplateRequest request, Guid userId)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var template = await _bookingTemplateRepository.GetByIdAsync(templateId);
        if (template == null || template.UserId != userId)
        {
            throw new KeyNotFoundException("Booking template not found or unauthorized.");
        }

        var vehicleId = request.VehicleId ?? template.VehicleId;
        if (vehicleId == null)
        {
            throw new InvalidOperationException("VehicleId must be specified in the template or request.");
        }

        var hasAccess = await _bookingRepository.UserHasVehicleAccessAsync(vehicleId.Value, userId);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to the specified vehicle.");
        }

        // Get vehicle to ensure it exists and to get GroupId
        var vehicle = await _bookingRepository.GetVehicleByIdAsync(vehicleId.Value)
                      ?? throw new InvalidOperationException($"Vehicle with ID {vehicleId} not found.");

        if (vehicle.GroupId == null)
        {
            throw new InvalidOperationException($"Vehicle with ID {vehicleId} is not associated with a group.");
        }

        var startAtUtc = NormalizeToUtc(request.StartDateTime);
        var endAtUtc = startAtUtc.Add(template.Duration);

        // Check for conflicts
        var conflicts = await _bookingRepository.GetConflictingBookingsAsync(
            vehicleId.Value,
            startAtUtc,
            endAtUtc,
            null // No booking to exclude for a new booking
        );

        if (conflicts.Any())
        {
            throw new InvalidOperationException("Booking conflicts with an existing booking.");
        }

        var booking = new CoOwnershipVehicle.Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId.Value,
            GroupId = vehicle.GroupId.Value, // Use GroupId from the vehicle
            UserId = userId,
            StartAt = startAtUtc,
            EndAt = endAtUtc,
            Notes = template.Notes,
            Purpose = template.Purpose,
            IsEmergency = false, // Templates are not for emergency bookings
            Priority = template.Priority,
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            BookingTemplateId = template.Id // Link to the template
        };

        await _bookingRepository.AddAsync(booking);

        // Increment usage count
        template.UsageCount++;
        template.UpdatedAt = DateTime.UtcNow;
        await _bookingTemplateRepository.UpdateAsync(template);

        await _bookingTemplateRepository.SaveChangesAsync(); // Save template and booking in one go (assuming shared DbContext or unit of work)

        await _publishEndpoint.Publish(new BookingCreatedEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = booking.UserId,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt,
            Status = booking.Status,
            IsEmergency = booking.IsEmergency,
            Priority = booking.Priority
        });

        _logger.LogInformation("Created booking {BookingId} from template {TemplateId} for user {UserId}.", booking.Id, template.Id, userId);

        return MapToBookingDto(booking);
    }

    public async Task<BookingTemplateResponse?> UpdateBookingTemplateAsync(Guid templateId, UpdateBookingTemplateRequest request, Guid userId)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var template = await _bookingTemplateRepository.GetByIdAsync(templateId);
        if (template == null || template.UserId != userId)
        {
            return null;
        }

        if (request.Name != null)
        {
            template.Name = request.Name;
        }

        if (request.VehicleId.HasValue)
        {
            var hasAccess = await _bookingRepository.UserHasVehicleAccessAsync(request.VehicleId.Value, userId);
            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("You do not have access to the specified vehicle.");
            }

            template.VehicleId = request.VehicleId;
        }

        if (request.Duration.HasValue)
        {
            if (request.Duration.Value <= TimeSpan.Zero)
            {
                throw new ArgumentException("Duration must be greater than zero.", nameof(request.Duration));
            }

            template.Duration = request.Duration.Value;
        }

        if (request.PreferredStartTime.HasValue)
        {
            if (request.PreferredStartTime.Value < TimeSpan.Zero || request.PreferredStartTime.Value >= TimeSpan.FromDays(1))
            {
                throw new ArgumentException("PreferredStartTime must fall within a 24-hour range.", nameof(request.PreferredStartTime));
            }

            template.PreferredStartTime = request.PreferredStartTime.Value;
        }

        if (request.Purpose != null)
        {
            template.Purpose = request.Purpose;
        }

        if (request.Notes != null)
        {
            template.Notes = request.Notes;
        }

        if (request.Priority.HasValue)
        {
            template.Priority = request.Priority.Value;
        }

        template.UpdatedAt = DateTime.UtcNow;

        await _bookingTemplateRepository.UpdateAsync(template);
        await _bookingTemplateRepository.SaveChangesAsync();

        _logger.LogInformation("Updated booking template {TemplateId} for user {UserId}.", template.Id, userId);

        return MapToDto(template);
    }

    public async Task DeleteBookingTemplateAsync(Guid templateId, Guid userId)
    {
        var template = await _bookingTemplateRepository.GetByIdAsync(templateId);
        if (template == null || template.UserId != userId)
        {
            throw new KeyNotFoundException("Booking template not found or unauthorized.");
        }

        await _bookingTemplateRepository.DeleteAsync(template);
        await _bookingTemplateRepository.SaveChangesAsync();

        _logger.LogInformation("Deleted booking template {TemplateId} for user {UserId}.", template.Id, userId);
    }

    private static BookingTemplateResponse MapToDto(BookingTemplate template)
    {
        return new BookingTemplateResponse
        {
            Id = template.Id,
            UserId = template.UserId,
            Name = template.Name,
            VehicleId = template.VehicleId,
            Duration = template.Duration,
            PreferredStartTime = template.PreferredStartTime,
            Purpose = template.Purpose,
            Notes = template.Notes,
            Priority = template.Priority,
            UsageCount = template.UsageCount,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    private static BookingDto MapToBookingDto(CoOwnershipVehicle.Domain.Entities.Booking booking)
    {
        return new BookingDto
        {
            Id = booking.Id,
            VehicleId = booking.VehicleId,
            GroupId = booking.GroupId,
            UserId = booking.UserId,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt,
            Notes = booking.Notes,
            Purpose = booking.Purpose,
            Status = booking.Status,
            IsEmergency = booking.IsEmergency,
            Priority = booking.Priority,
            CreatedAt = booking.CreatedAt,
        };
    }

    private static DateTime NormalizeToUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }
}

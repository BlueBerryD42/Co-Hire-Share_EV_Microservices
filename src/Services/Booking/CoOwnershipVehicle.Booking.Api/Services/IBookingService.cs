using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace CoOwnershipVehicle.Booking.Api.Services;

public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(CreateBookingDto createDto, Guid userId);
    Task<List<BookingDto>> GetUserBookingsAsync(Guid userId, DateTime? from = null, DateTime? to = null);
    Task<List<BookingDto>> GetVehicleBookingsAsync(Guid vehicleId, DateTime? from = null, DateTime? to = null);
    Task<BookingConflictDto> CheckBookingConflictsAsync(Guid vehicleId, DateTime startAt, DateTime endAt, Guid? excludeBookingId = null);
    Task<List<BookingPriorityDto>> GetBookingPriorityQueueAsync(Guid vehicleId, DateTime startAt, DateTime endAt);
    Task<BookingDto> ApproveBookingAsync(Guid bookingId, Guid approverId);
    Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, string? reason = null);
    Task<List<BookingDto>> GetPendingApprovalsAsync(Guid userId);
}

public class BookingService : IBookingService
{
    private readonly BookingDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        BookingDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<BookingService> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<BookingDto> CreateBookingAsync(CreateBookingDto createDto, Guid userId)
    {
        // Validate user has access to the vehicle
        var hasAccess = await _context.Vehicles
            .Include(v => v.Group)
                .ThenInclude(g => g!.Members)
            .AnyAsync(v => v.Id == createDto.VehicleId && 
                          v.Group != null && 
                          v.Group.Members.Any(m => m.UserId == userId));

        if (!hasAccess)
            throw new UnauthorizedAccessException("Access denied to this vehicle");

        // Check for conflicts
        var conflicts = await CheckBookingConflictsAsync(createDto.VehicleId, createDto.StartAt, createDto.EndAt);
        
        if (conflicts.HasConflicts)
        {
            // Get user's priority in the group
            var userPriority = (Domain.Entities.BookingPriority)await GetUserPriorityAsync(userId, createDto.VehicleId);
            
            // If user has higher priority, mark as priority request
            var requiresApproval = conflicts.ConflictingBookings.Any(cb => cb.Priority >= userPriority);
            
            if (requiresApproval && !createDto.IsEmergency)
            {
                // Create booking with pending approval status
                return await CreatePendingBookingAsync(createDto, userId, conflicts);
            }
            
            if (!createDto.IsEmergency)
            {
                throw new InvalidOperationException($"Booking conflicts detected with {conflicts.ConflictingBookings.Count} existing bookings");
            }
        }

        // Create approved booking
        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            VehicleId = createDto.VehicleId,
            UserId = userId,
            StartAt = createDto.StartAt,
            EndAt = createDto.EndAt,
            Purpose = createDto.Purpose,
            Notes = createDto.Notes,
            IsEmergency = createDto.IsEmergency,
            Priority = (Domain.Entities.BookingPriority)await GetUserPriorityAsync(userId, createDto.VehicleId),
            Status = Domain.Entities.BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Publish booking created event
        await _publishEndpoint.Publish(new BookingCreatedEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = booking.UserId,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt,
            Status = BookingStatus.Confirmed,
            IsEmergency = booking.IsEmergency,
            Priority = booking.Priority
        });

        _logger.LogInformation("Booking {BookingId} created for vehicle {VehicleId} by user {UserId}", 
            booking.Id, booking.VehicleId, userId);

        return await GetBookingByIdAsync(booking.Id);
    }

    public async Task<List<BookingDto>> GetUserBookingsAsync(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Bookings
            .Include(b => b.Vehicle)
                .ThenInclude(v => v!.Group)
            .Include(b => b.User)
            .Where(b => b.UserId == userId);

        if (from.HasValue)
            query = query.Where(b => b.EndAt >= from.Value);

        if (to.HasValue)
            query = query.Where(b => b.StartAt <= to.Value);

        return await query
            .OrderBy(b => b.StartAt)
            .Select(b => new BookingDto
            {
                Id = b.Id,
                VehicleId = b.VehicleId,
                VehicleModel = b.Vehicle!.Model,
                VehiclePlateNumber = b.Vehicle.PlateNumber,
                UserId = b.UserId,
                UserFirstName = b.User.FirstName,
                UserLastName = b.User.LastName,
                StartAt = b.StartAt,
                EndAt = b.EndAt,
                Purpose = b.Purpose,
                Notes = b.Notes,
                Status = (BookingStatus)b.Status,
                Priority = b.Priority,
                IsEmergency = b.IsEmergency,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<BookingDto>> GetVehicleBookingsAsync(Guid vehicleId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Bookings
            .Include(b => b.User)
            .Where(b => b.VehicleId == vehicleId && b.Status != Domain.Entities.BookingStatus.Cancelled);

        if (from.HasValue)
            query = query.Where(b => b.EndAt >= from.Value);

        if (to.HasValue)
            query = query.Where(b => b.StartAt <= to.Value);

        return await query
            .OrderBy(b => b.StartAt)
            .Select(b => new BookingDto
            {
                Id = b.Id,
                VehicleId = b.VehicleId,
                UserId = b.UserId,
                UserFirstName = b.User.FirstName,
                UserLastName = b.User.LastName,
                StartAt = b.StartAt,
                EndAt = b.EndAt,
                Purpose = b.Purpose,
                Notes = b.Notes,
                Status = (BookingStatus)b.Status,
                Priority = b.Priority,
                IsEmergency = b.IsEmergency,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<BookingConflictDto> CheckBookingConflictsAsync(Guid vehicleId, DateTime startAt, DateTime endAt, Guid? excludeBookingId = null)
    {
        // Check conflicting bookings
        var conflictingBookings = await _context.Bookings
            .Include(b => b.User)
            .Where(b => b.VehicleId == vehicleId &&
                       b.Status != Domain.Entities.BookingStatus.Cancelled &&
                       b.Status != Domain.Entities.BookingStatus.Completed &&
                       (excludeBookingId == null || b.Id != excludeBookingId) &&
                       ((b.StartAt <= startAt && b.EndAt > startAt) ||
                        (b.StartAt < endAt && b.EndAt >= endAt) ||
                        (b.StartAt >= startAt && b.EndAt <= endAt)))
            .Select(b => new BookingDto
            {
                Id = b.Id,
                VehicleId = b.VehicleId,
                UserId = b.UserId,
                UserFirstName = b.User.FirstName,
                UserLastName = b.User.LastName,
                StartAt = b.StartAt,
                EndAt = b.EndAt,
                Status = (BookingStatus)b.Status,
                Priority = b.Priority,
                IsEmergency = b.IsEmergency
            })
            .ToListAsync();

        // Check for maintenance blocks (calendar blocks)
        var maintenanceBlocks = await _context.MaintenanceBlocks
            .Where(m => m.VehicleId == vehicleId &&
                       m.Status != Domain.Enums.MaintenanceStatus.Cancelled &&
                       m.Status != Domain.Enums.MaintenanceStatus.Completed &&
                       ((m.StartTime <= startAt && m.EndTime > startAt) ||
                        (m.StartTime < endAt && m.EndTime >= endAt) ||
                        (m.StartTime >= startAt && m.EndTime <= endAt)))
            .ToListAsync();

        // If there are maintenance blocks, throw exception to prevent booking
        if (maintenanceBlocks.Any())
        {
            var block = maintenanceBlocks.First();
            throw new InvalidOperationException(
                $"Cannot create booking: Vehicle is scheduled for maintenance ({block.ServiceType}) from {block.StartTime:yyyy-MM-dd HH:mm} to {block.EndTime:HH:mm}");
        }

        return new BookingConflictDto
        {
            VehicleId = vehicleId,
            RequestedStartAt = startAt,
            RequestedEndAt = endAt,
            HasConflicts = conflictingBookings.Any(),
            ConflictingBookings = conflictingBookings
        };
    }

    public async Task<List<BookingPriorityDto>> GetBookingPriorityQueueAsync(Guid vehicleId, DateTime startAt, DateTime endAt)
    {
        // Get all bookings in the time range including pending ones
        var bookings = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Vehicle)
                .ThenInclude(v => v!.Group)
                    .ThenInclude(g => g!.Members)
            .Where(b => b.VehicleId == vehicleId &&
                       b.Status != Domain.Entities.BookingStatus.Cancelled &&
                       b.Status != Domain.Entities.BookingStatus.Completed &&
                       ((b.StartAt <= startAt && b.EndAt > startAt) ||
                        (b.StartAt < endAt && b.EndAt >= endAt) ||
                        (b.StartAt >= startAt && b.EndAt <= endAt)))
            .ToListAsync();

        // Calculate priority scores
        var priorityQueue = bookings.Select(b => new BookingPriorityDto
        {
            BookingId = b.Id,
            UserId = b.UserId,
            UserName = $"{b.User.FirstName} {b.User.LastName}",
            VehicleId = b.VehicleId,
            StartAt = b.StartAt,
            EndAt = b.EndAt,
            Status = (BookingStatus)b.Status,
            Priority = (int)b.Priority,
            IsEmergency = b.IsEmergency,
            PriorityScore = CalculatePriorityScore(b),
            OwnershipPercentage = GetUserOwnershipPercentage(b.UserId, b.Vehicle!.Group!.Members)
        })
        .OrderByDescending(p => p.PriorityScore)
        .ThenByDescending(p => p.OwnershipPercentage)
        .ThenBy(p => p.StartAt)
        .ToList();

        return priorityQueue;
    }

    public async Task<BookingDto> ApproveBookingAsync(Guid bookingId, Guid approverId)
    {
        var booking = await _context.Bookings
            .Include(b => b.Vehicle)
                .ThenInclude(v => v!.Group)
                    .ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            throw new ArgumentException("Booking not found");

        // Verify approver has admin rights to the group
        var isAdmin = booking.Vehicle!.Group!.Members
            .Any(m => m.UserId == approverId && m.RoleInGroup == Domain.Entities.GroupRole.Admin);

        if (!isAdmin)
            throw new UnauthorizedAccessException("Only group admins can approve bookings");

        booking.Status = Domain.Entities.BookingStatus.Confirmed;
        booking.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Publish booking approved event
        await _publishEndpoint.Publish(new BookingApprovedEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = booking.UserId,
            ApprovedBy = approverId,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt
        });

        _logger.LogInformation("Booking {BookingId} approved by {ApproverId}", bookingId, approverId);

        return await GetBookingByIdAsync(booking.Id);
    }

    public async Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, string? reason = null)
    {
        var booking = await _context.Bookings
            .Include(b => b.Vehicle)
                .ThenInclude(v => v!.Group)
                    .ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            throw new ArgumentException("Booking not found");

        // User can cancel their own booking or group admin can cancel any booking
        var isOwner = booking.UserId == userId;
        var isAdmin = booking.Vehicle!.Group!.Members
            .Any(m => m.UserId == userId && m.RoleInGroup == Domain.Entities.GroupRole.Admin);

        if (!isOwner && !isAdmin)
            throw new UnauthorizedAccessException("Cannot cancel this booking");

        booking.Status = Domain.Entities.BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        booking.Notes = !string.IsNullOrEmpty(reason) 
            ? $"{booking.Notes}\n[CANCELLED] {reason}"
            : $"{booking.Notes}\n[CANCELLED]";

        await _context.SaveChangesAsync();

        // Publish booking cancelled event
        await _publishEndpoint.Publish(new BookingCancelledEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = booking.UserId,
            CancelledBy = userId,
            Reason = reason,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt
        });

        _logger.LogInformation("Booking {BookingId} cancelled by {UserId}", bookingId, userId);

        return await GetBookingByIdAsync(booking.Id);
    }

    public async Task<List<BookingDto>> GetPendingApprovalsAsync(Guid userId)
    {
        // Get vehicles where user is group admin
        var adminVehicleIds = await _context.Vehicles
            .Include(v => v.Group)
                .ThenInclude(g => g!.Members)
            .Where(v => v.Group != null && 
                       v.Group.Members.Any(m => m.UserId == userId && m.RoleInGroup == Domain.Entities.GroupRole.Admin))
            .Select(v => v.Id)
            .ToListAsync();

        return await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Vehicle)
            .Where(b => adminVehicleIds.Contains(b.VehicleId) && 
                       b.Status == Domain.Entities.BookingStatus.PendingApproval)
            .OrderBy(b => b.CreatedAt)
            .Select(b => new BookingDto
            {
                Id = b.Id,
                VehicleId = b.VehicleId,
                VehicleModel = b.Vehicle!.Model,
                VehiclePlateNumber = b.Vehicle.PlateNumber,
                UserId = b.UserId,
                UserFirstName = b.User.FirstName,
                UserLastName = b.User.LastName,
                StartAt = b.StartAt,
                EndAt = b.EndAt,
                Purpose = b.Purpose,
                Notes = b.Notes,
                Status = (BookingStatus)b.Status,
                Priority = b.Priority,
                IsEmergency = b.IsEmergency,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();
    }

    private async Task<BookingDto> CreatePendingBookingAsync(CreateBookingDto createDto, Guid userId, BookingConflictDto conflicts)
    {
        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            VehicleId = createDto.VehicleId,
            UserId = userId,
            StartAt = createDto.StartAt,
            EndAt = createDto.EndAt,
            Purpose = createDto.Purpose,
            Notes = createDto.Notes,
            IsEmergency = createDto.IsEmergency,
            Priority = (Domain.Entities.BookingPriority)await GetUserPriorityAsync(userId, createDto.VehicleId),
            Status = Domain.Entities.BookingStatus.PendingApproval,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Publish booking pending approval event
        await _publishEndpoint.Publish(new BookingPendingApprovalEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = booking.UserId,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt,
            ConflictCount = conflicts.ConflictingBookings.Count
        });

        return await GetBookingByIdAsync(booking.Id);
    }

    private async Task<int> GetUserPriorityAsync(Guid userId, Guid vehicleId)
    {
        // Priority based on ownership percentage and role
        var member = await _context.GroupMembers
            .Include(m => m.Group)
                .ThenInclude(g => g.Vehicles)
            .FirstOrDefaultAsync(m => m.UserId == userId && 
                                    m.Group.Vehicles.Any(v => v.Id == vehicleId));

        if (member == null) return 0;

        var basePriority = (int)(member.SharePercentage * 100); // 0-100 based on ownership
        var rolePriority = member.RoleInGroup == Domain.Entities.GroupRole.Admin ? 50 : 0;

        return basePriority + rolePriority;
    }

    private int CalculatePriorityScore(Domain.Entities.Booking booking)
    {
        var score = (int)booking.Priority; // Base priority from ownership

        // Emergency bookings get highest priority
        if (booking.IsEmergency)
            score += 1000;

        // Confirmed bookings have higher priority than pending
        if (booking.Status == Domain.Entities.BookingStatus.Confirmed)
            score += 100;

        // Earlier bookings have slight priority (first-come-first-served within same priority)
        var daysSinceCreated = (DateTime.UtcNow - booking.CreatedAt).Days;
        score += Math.Max(0, 30 - daysSinceCreated); // Up to 30 days advantage

        return score;
    }

    private decimal GetUserOwnershipPercentage(Guid userId, ICollection<Domain.Entities.GroupMember> members)
    {
        return members.FirstOrDefault(m => m.UserId == userId)?.SharePercentage ?? 0;
    }

    private async Task<BookingDto> GetBookingByIdAsync(Guid bookingId)
    {
        return await _context.Bookings
            .Include(b => b.Vehicle)
            .Include(b => b.User)
            .Where(b => b.Id == bookingId)
            .Select(b => new BookingDto
            {
                Id = b.Id,
                VehicleId = b.VehicleId,
                VehicleModel = b.Vehicle!.Model,
                VehiclePlateNumber = b.Vehicle.PlateNumber,
                UserId = b.UserId,
                UserFirstName = b.User.FirstName,
                UserLastName = b.User.LastName,
                StartAt = b.StartAt,
                EndAt = b.EndAt,
                Purpose = b.Purpose,
                Notes = b.Notes,
                Status = (BookingStatus)b.Status,
                Priority = b.Priority,
                IsEmergency = b.IsEmergency,
                CreatedAt = b.CreatedAt
            })
            .FirstAsync();
    }
}

// Supporting DTOs
public class CreateBookingDto
{
    public Guid VehicleId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsEmergency { get; set; }
}

public class BookingConflictDto
{
    public Guid VehicleId { get; set; }
    public DateTime RequestedStartAt { get; set; }
    public DateTime RequestedEndAt { get; set; }
    public bool HasConflicts { get; set; }
    public List<BookingDto> ConflictingBookings { get; set; } = new();
}

public class BookingPriorityDto
{
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public BookingStatus Status { get; set; }
    public int Priority { get; set; }
    public bool IsEmergency { get; set; }
    public int PriorityScore { get; set; }
    public decimal OwnershipPercentage { get; set; }
}

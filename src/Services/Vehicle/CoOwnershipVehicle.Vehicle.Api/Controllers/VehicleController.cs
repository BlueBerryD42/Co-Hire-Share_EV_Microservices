using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Vehicle.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehicleController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VehicleController> _logger;

    public VehicleController(ApplicationDbContext context, ILogger<VehicleController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get vehicles for user's groups
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetVehicles()
    {
        try
        {
            var userId = GetCurrentUserId();
            
            var vehicles = await _context.Vehicles
                .Include(v => v.Group)
                    .ThenInclude(g => g!.Members)
                .Where(v => v.Group != null && v.Group.Members.Any(m => m.UserId == userId))
                .Select(v => new VehicleDto
                {
                    Id = v.Id,
                    Vin = v.Vin,
                    PlateNumber = v.PlateNumber,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color,
                    Status = (VehicleStatus)v.Status,
                    LastServiceDate = v.LastServiceDate,
                    Odometer = v.Odometer,
                    GroupId = v.GroupId,
                    GroupName = v.Group!.Name,
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync();

            return Ok(vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicles");
            return StatusCode(500, new { message = "An error occurred while retrieving vehicles" });
        }
    }

    /// <summary>
    /// Get vehicle by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVehicle(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            var vehicle = await _context.Vehicles
                .Include(v => v.Group)
                    .ThenInclude(g => g!.Members)
                .Where(v => v.Id == id && v.Group != null && v.Group.Members.Any(m => m.UserId == userId))
                .Select(v => new VehicleDto
                {
                    Id = v.Id,
                    Vin = v.Vin,
                    PlateNumber = v.PlateNumber,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color,
                    Status = (VehicleStatus)v.Status,
                    LastServiceDate = v.LastServiceDate,
                    Odometer = v.Odometer,
                    GroupId = v.GroupId,
                    GroupName = v.Group!.Name,
                    CreatedAt = v.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (vehicle == null)
                return NotFound(new { message = "Vehicle not found or access denied" });

            return Ok(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicle {VehicleId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving vehicle" });
        }
    }

    /// <summary>
    /// Create a new vehicle (Group Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupAdmin")]
    public async Task<IActionResult> CreateVehicle([FromBody] CreateVehicleDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();

            // Verify user has admin rights to the group
            var hasGroupAccess = await _context.GroupMembers
                .AnyAsync(m => m.UserId == userId && 
                             m.GroupId == createDto.GroupId && 
                             m.RoleInGroup == Domain.Entities.GroupRole.Admin);

            if (!hasGroupAccess)
                return Forbidden(new { message = "Insufficient permissions to create vehicle for this group" });

            // Check if VIN already exists
            var existingVin = await _context.Vehicles.AnyAsync(v => v.Vin == createDto.Vin);
            if (existingVin)
                return BadRequest(new { message = "Vehicle with this VIN already exists" });

            // Check if plate number already exists
            var existingPlate = await _context.Vehicles.AnyAsync(v => v.PlateNumber == createDto.PlateNumber);
            if (existingPlate)
                return BadRequest(new { message = "Vehicle with this plate number already exists" });

            var vehicle = new Domain.Entities.Vehicle
            {
                Id = Guid.NewGuid(),
                Vin = createDto.Vin,
                PlateNumber = createDto.PlateNumber,
                Model = createDto.Model,
                Year = createDto.Year,
                Color = createDto.Color,
                Status = Domain.Entities.VehicleStatus.Available,
                Odometer = createDto.Odometer,
                GroupId = createDto.GroupId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Vehicle {VehicleId} created by user {UserId}", vehicle.Id, userId);

            return await GetVehicle(vehicle.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle");
            return StatusCode(500, new { message = "An error occurred while creating vehicle" });
        }
    }

    /// <summary>
    /// Check vehicle availability
    /// </summary>
    [HttpGet("{id:guid}/availability")]
    public async Task<IActionResult> CheckAvailability(Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Verify access to vehicle
            var hasAccess = await _context.Vehicles
                .Include(v => v.Group)
                    .ThenInclude(g => g!.Members)
                .AnyAsync(v => v.Id == id && v.Group != null && v.Group.Members.Any(m => m.UserId == userId));

            if (!hasAccess)
                return NotFound(new { message = "Vehicle not found or access denied" });

            // Check for conflicting bookings
            var conflictingBookings = await _context.Bookings
                .Include(b => b.User)
                .Where(b => b.VehicleId == id && 
                           b.Status != Domain.Entities.BookingStatus.Cancelled &&
                           b.Status != Domain.Entities.BookingStatus.Completed &&
                           ((b.StartAt <= from && b.EndAt > from) ||
                            (b.StartAt < to && b.EndAt >= to) ||
                            (b.StartAt >= from && b.EndAt <= to)))
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
                    Notes = b.Notes
                })
                .ToListAsync();

            var availability = new VehicleAvailabilityDto
            {
                VehicleId = id,
                From = from,
                To = to,
                IsAvailable = !conflictingBookings.Any(),
                ConflictingBookings = conflictingBookings
            };

            return Ok(availability);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking vehicle availability {VehicleId}", id);
            return StatusCode(500, new { message = "An error occurred while checking availability" });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    private IActionResult Forbidden(object value)
    {
        return StatusCode(403, value);
    }
}

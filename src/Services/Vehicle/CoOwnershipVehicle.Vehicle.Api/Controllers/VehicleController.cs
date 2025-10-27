using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Vehicle.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehicleController : ControllerBase
{
    private readonly VehicleDbContext _context;
    private readonly ILogger<VehicleController> _logger;

    public VehicleController(VehicleDbContext context, ILogger<VehicleController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all vehicles (temporary placeholder)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetVehicles()
    {
        // Placeholder: Returns all vehicles from this service's DB
        var vehicles = await _context.Vehicles.ToListAsync();
        return Ok(vehicles);
    }

    /// <summary>
    /// Get vehicle by ID (temporary placeholder)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVehicle(Guid id)
    {
        // Placeholder: Returns vehicle by ID from this service's DB
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();
        return Ok(vehicle);
    }

    /// <summary>
    /// Create a new vehicle (Group Admin or SystemAdmin only)
    /// </summary>
    [HttpPost]
    [Authorize] // Role check is now done manually inside the method
    public async Task<IActionResult> CreateVehicle([FromBody] CreateVehicleDto createDto)
    {
        // ===== START DIAGNOSTIC LOG =====
        _logger.LogInformation("--- DIAGNOSTIC LOG: CLAIMS RECEIVED ---");
        foreach (var claim in User.Claims)
        {
            _logger.LogInformation("Claim Type: {type}, Claim Value: {value}", claim.Type, claim.Value);
        }
        _logger.LogInformation("--- END DIAGNOSTIC LOG ---");
        // ===== END DIAGNOSTIC LOG =====

        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();

            // Ultra-robust, manual role check that checks for both short and long role claim types
            var roles = User.Claims
                .Where(c => c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value);

            var isSystemAdmin = roles.Contains("SystemAdmin");
            var isGroupAdmin = roles.Contains("GroupAdmin");

            if (!isSystemAdmin && !isGroupAdmin)
            {
                 return Forbidden(new { message = "Insufficient permissions to create vehicle for this group" });
            }

            // Check if VIN already exists
            var existingVin = await _context.Vehicles.AnyAsync(v => v.Vin == createDto.Vin);
            if (existingVin)
                return BadRequest(new { message = "Vehicle with this VIN already exists" });

            // Check if PlateNumber already exists
            var existingPlateNumber = await _context.Vehicles.AnyAsync(v => v.PlateNumber == createDto.PlateNumber);
            if (existingPlateNumber)
                return BadRequest(new { message = "Vehicle with this PlateNumber already exists" });

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

            // Return a simple response
            return CreatedAtAction(nameof(GetVehicle), new { id = vehicle.Id }, vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle");
            return StatusCode(500, new { message = "An error occurred while creating vehicle" });
        }
    }

    /// <summary>
    /// Check vehicle availability (temporary placeholder)
    /// </summary>
    [HttpGet("{id:guid}/availability")]
    public async Task<IActionResult> CheckAvailability(Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        // Placeholder: This method requires access to the Booking service's data.
        return StatusCode(501, "Not Implemented");
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

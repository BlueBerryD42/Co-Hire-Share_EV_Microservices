using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Vehicle.Api.Services;
using CoOwnershipVehicle.Vehicle.Api.DTOs; // Added for IGroupServiceClient

namespace CoOwnershipVehicle.Vehicle.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehicleController : ControllerBase
{
    private readonly VehicleDbContext _context;
    private readonly ILogger<VehicleController> _logger;
    private readonly IGroupServiceClient _groupServiceClient; // Injected
    private readonly IBookingServiceClient _bookingServiceClient; // Injected
    private readonly VehicleStatisticsService _statisticsService; // Injected
    private readonly CostAnalysisService _costAnalysisService; // Injected
    private readonly MemberUsageService _memberUsageService; // Injected
    private readonly VehicleHealthScoreService _healthScoreService; // Injected

    public VehicleController(
        VehicleDbContext context,
        ILogger<VehicleController> logger,
        IGroupServiceClient groupServiceClient,
        IBookingServiceClient bookingServiceClient,
        VehicleStatisticsService statisticsService,
        CostAnalysisService costAnalysisService,
        MemberUsageService memberUsageService,
        VehicleHealthScoreService healthScoreService)
    {
        _context = context;
        _logger = logger;
        _groupServiceClient = groupServiceClient; // Assigned
        _bookingServiceClient = bookingServiceClient; // Assigned
        _statisticsService = statisticsService; // Assigned
        _costAnalysisService = costAnalysisService; // Assigned
        _memberUsageService = memberUsageService; // Assigned
        _healthScoreService = healthScoreService; // Assigned
    }

    /// <summary>
    /// Get all vehicles accessible to the current user, including health scores
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VehicleListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVehicles()
    {
        var userGroupIds = await GetUserGroupIds(); // Await the async method

        if (!userGroupIds.Any())
        {
            // If the user is not part of any group, they shouldn't see any vehicles
            return Ok(new List<VehicleListItemDto>());
        }

        var vehicles = await _context.Vehicles
                                     .Where(v => userGroupIds.Contains((Guid)v.GroupId))
                                     .ToListAsync();

        // Map to DTOs and include health scores
        var vehicleList = new List<VehicleListItemDto>();

        foreach (var vehicle in vehicles)
        {
            var healthSummary = await _healthScoreService.GetHealthSummaryAsync(vehicle.Id);

            vehicleList.Add(new VehicleListItemDto
            {
                Id = vehicle.Id,
                Vin = vehicle.Vin,
                PlateNumber = vehicle.PlateNumber,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Color = vehicle.Color,
                Status = vehicle.Status.ToString(),
                LastServiceDate = vehicle.LastServiceDate,
                Odometer = vehicle.Odometer,
                GroupId = vehicle.GroupId,
                CreatedAt = vehicle.CreatedAt,
                UpdatedAt = vehicle.UpdatedAt,
                HealthScore = healthSummary
            });
        }

        return Ok(vehicleList);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVehicle(Guid id)
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null) return NotFound();

        var userGroupIds = await GetUserGroupIds(); // Await the async method
        if (!userGroupIds.Contains((Guid)vehicle.GroupId))
        {
            return Forbidden(new { message = "You do not have permission to access this vehicle." });
        }

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
        var accessToken = HttpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(accessToken))
        {
            return Unauthorized(new { message = "Access token not found" });
        }

        var conflicts = await _bookingServiceClient.CheckAvailabilityAsync(id, from, to, accessToken);

        if (conflicts == null)
        {
            return StatusCode(500, new { message = "An error occurred while checking availability" });
        }

        return Ok(new { Available = !conflicts.HasConflicts, Conflicts = conflicts.ConflictingBookings });
    }

    /// <summary>
/// Update the status of a vehicle (Group Admin or SystemAdmin only)
/// </summary>
[HttpPut("{id:guid}/status")]
[Authorize]
public async Task<IActionResult> UpdateVehicleStatus(Guid id, [FromBody] UpdateVehicleStatusDto updateDto)
{
    try
    {
        var vehicle = await _context.Vehicles.FindAsync(id);
        if (vehicle == null)
            return NotFound();

        var userId = GetCurrentUserId();
        var roles = User.Claims
            .Where(c => c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var isSystemAdmin = roles.Contains("SystemAdmin");
        var isGroupAdmin = roles.Contains("GroupAdmin");

        // Quyền hạn kiểm tra trước
        if (!isSystemAdmin && !isGroupAdmin)
        {
            return Forbid("Insufficient permissions to update vehicle status");
        }

        //  (Tuỳ chọn) — kiểm tra xung đột booking nếu DTO có khoảng thời gian
        if (updateDto.From != null && updateDto.To != null)
        {
            // Get access token for Booking Service call
            var accessToken = GetAccessToken();
            
            // Check for conflicting bookings via Booking Service
            var conflictCheck = await _bookingServiceClient.CheckAvailabilityAsync(
                id, updateDto.From.Value, updateDto.To.Value, accessToken);

            if (conflictCheck != null && conflictCheck.HasConflicts && conflictCheck.ConflictingBookings.Any())
            {
                // Map Vehicle.Api.DTOs.BookingDto to Shared.Contracts.DTOs.BookingDto
                var conflictingBookings = conflictCheck.ConflictingBookings.Select(b => new CoOwnershipVehicle.Shared.Contracts.DTOs.BookingDto
                {
                    Id = b.Id,
                    VehicleId = b.VehicleId,
                    UserId = b.UserId,
                    UserFirstName = b.UserFirstName ?? string.Empty,
                    UserLastName = b.UserLastName ?? string.Empty,
                    StartAt = b.StartAt,
                    EndAt = b.EndAt,
                    Status = MapBookingStatus(b.Status),
                    Notes = null, // Not available in conflict DTO
                    RequiresDamageReview = false // Not available in conflict DTO
                }).ToList();

                return Conflict(new
                {
                    message = "Cannot update status due to active or overlapping bookings",
                    conflictingBookings
                });
            }
        }

        //  Cập nhật trạng thái
        vehicle.Status = updateDto.Status;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Vehicle {VehicleId} status updated to {Status} by user {UserId}", vehicle.Id, vehicle.Status, userId);

        return Ok(vehicle);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating vehicle status");
        return StatusCode(500, new { message = "An error occurred while updating vehicle status" });
    }
}


    private async Task<List<Guid>> GetUserGroupIds() // Made async
    {
        var userId = GetCurrentUserId();
        var accessToken = HttpContext.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("Access token not found for user {UserId}", userId);
            return new List<Guid>();
        }

        try
        {
            var groups = await _groupServiceClient.GetUserGroups(accessToken);
            return groups.Select(g => g.Id).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user groups for user {UserId}", userId);
            return new List<Guid>();
        }
    }

    /// <summary>
    /// Update the odometer of a vehicle (Group Admin or SystemAdmin only)
    /// </summary>
    [HttpPut("{id:guid}/odometer")]
    [Authorize]
    public async Task<IActionResult> UpdateOdometer(Guid id, [FromBody] UpdateOdometerDto updateDto)
    {
        try
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null) return NotFound();

            var userId = GetCurrentUserId();
            var roles = User.Claims
                .Where(c => c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value);

            var isSystemAdmin = roles.Contains("SystemAdmin");
            var isGroupAdmin = roles.Contains("GroupAdmin");

            if (!isSystemAdmin && !isGroupAdmin)
            {
                return Forbidden(new { message = "Insufficient permissions to update vehicle odometer" });
            }

            if (updateDto.Odometer < vehicle.Odometer)
            {
                return BadRequest(new { message = "New odometer reading cannot be less than current reading" });
            }

            vehicle.Odometer = updateDto.Odometer;
            vehicle.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Vehicle {VehicleId} odometer updated to {Odometer} by user {UserId}", vehicle.Id, vehicle.Odometer, userId);

            return Ok(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle odometer");
            return StatusCode(500, new { message = "An error occurred while updating vehicle odometer" });
        }
    }

    /// <summary>
    /// Get comprehensive usage statistics for a vehicle
    /// </summary>
    /// <param name="id">Vehicle ID</param>
    /// <param name="startDate">Start date for statistics period (default: 30 days ago)</param>
    /// <param name="endDate">End date for statistics period (default: now)</param>
    /// <param name="groupBy">Time grouping: daily, weekly, monthly (default: daily)</param>
    /// <param name="includeBenchmarks">Include benchmark comparisons (default: true)</param>
    /// <returns>Comprehensive vehicle statistics including usage, utilization, efficiency, patterns, trends, and benchmarks</returns>
    [HttpGet("{id:guid}/statistics")]
    [ProducesResponseType(typeof(DTOs.VehicleStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVehicleStatistics(
        Guid id,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string groupBy = "daily",
        [FromQuery] bool includeBenchmarks = true)
    {
        try
        {
            // 1. Validate vehicle exists
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle {VehicleId} not found", id);
                return NotFound(new { message = $"Vehicle {id} not found" });
            }

            // 2. Check authorization - user must be in the vehicle's group
            var userId = GetCurrentUserId();
            var userGroupIds = await GetUserGroupIds();

            if (!vehicle.GroupId.HasValue || !userGroupIds.Contains(vehicle.GroupId.Value))
            {
                _logger.LogWarning("User {UserId} attempted to access statistics for vehicle {VehicleId} without authorization", userId, id);
                return Forbidden(new { message = "You do not have permission to view statistics for this vehicle" });
            }

            // 3. Get access token for Booking service calls
            var accessToken = GetAccessToken();

            // 4. Build request
            var request = new DTOs.VehicleStatisticsRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                GroupBy = groupBy,
                IncludeBenchmarks = includeBenchmarks
            };

            // 5. Get statistics from service
            var statistics = await _statisticsService.GetVehicleStatisticsAsync(id, request, accessToken);

            _logger.LogInformation(
                "Retrieved statistics for vehicle {VehicleId} for period {StartDate} to {EndDate}",
                id, statistics.StartDate, statistics.EndDate);

            return Ok(statistics);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Vehicle {VehicleId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to vehicle statistics");
            return Forbidden(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics for vehicle {VehicleId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving vehicle statistics" });
        }
    }

    /// <summary>
    /// Get comprehensive cost analysis for a vehicle
    /// </summary>
    /// <param name="id">Vehicle ID</param>
    /// <param name="startDate">Start date for analysis (optional, default: 1 year ago)</param>
    /// <param name="endDate">End date for analysis (optional, default: now)</param>
    /// <param name="groupBy">Grouping period: month, quarter, year (default: month)</param>
    /// <returns>Complete cost breakdown and analysis</returns>
    [HttpGet("{id:guid}/cost-analysis")]
    public async Task<IActionResult> GetCostAnalysis(
        Guid id,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string groupBy = "month")
    {
        try
        {
            // 1. Check if vehicle exists
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
            {
                return NotFound(new { message = $"Vehicle {id} not found" });
            }

            // 2. Check authorization - user must be in vehicle's group
            var userGroupIds = await GetUserGroupIds();
            if (!vehicle.GroupId.HasValue || !userGroupIds.Contains(vehicle.GroupId.Value))
            {
                return Forbidden(new { message = "You do not have permission to access this vehicle's cost analysis" });
            }

            // 3. Get access token for inter-service calls
            var accessToken = GetAccessToken();

            // 4. Build request
            var request = new CostAnalysisRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                GroupBy = groupBy
            };

            // 5. Get cost analysis from service
            var costAnalysis = await _costAnalysisService.GetCostAnalysisAsync(id, request, accessToken);

            return Ok(costAnalysis);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to cost analysis for vehicle {VehicleId}", id);
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for vehicle {VehicleId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cost analysis for vehicle {VehicleId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving cost analysis" });
        }
    }

    /// <summary>
    /// Get per-member usage analysis for a vehicle
    /// </summary>
    /// <param name="id">Vehicle ID</param>
    /// <param name="startDate">Start date for analysis (optional, default: 3 months ago)</param>
    /// <param name="endDate">End date for analysis (optional, default: now)</param>
    /// <returns>Member usage breakdown with fairness analysis</returns>
    [HttpGet("{id:guid}/member-usage")]
    public async Task<IActionResult> GetMemberUsage(
        Guid id,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            // 1. Check if vehicle exists
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
            {
                return NotFound(new { message = $"Vehicle {id} not found" });
            }

            // 2. Check authorization - user must be in vehicle's group
            var userGroupIds = await GetUserGroupIds();
            if (!vehicle.GroupId.HasValue || !userGroupIds.Contains(vehicle.GroupId.Value))
            {
                return Forbidden(new { message = "You do not have permission to access this vehicle's member usage data" });
            }

            // 3. Get access token for inter-service calls
            var accessToken = GetAccessToken();

            // 4. Build request
            var request = new MemberUsageRequest
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // 5. Get member usage analysis from service
            var memberUsage = await _memberUsageService.GetMemberUsageAsync(id, request, accessToken);

            return Ok(memberUsage);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to member usage for vehicle {VehicleId}", id);
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for vehicle {VehicleId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving member usage for vehicle {VehicleId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving member usage data" });
        }
    }

    /// <summary>
    /// Get comprehensive health score for a vehicle
    /// </summary>
    /// <param name="id">Vehicle ID</param>
    /// <param name="includeHistory">Include historical score trend (default: true)</param>
    /// <param name="includeBenchmark">Include benchmark comparison (default: true)</param>
    /// <param name="historyMonths">Number of months for historical data (default: 6)</param>
    /// <returns>Complete health score analysis with recommendations</returns>
    [HttpGet("{id:guid}/health-score")]
    [ProducesResponseType(typeof(HealthScoreResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetHealthScore(
        Guid id,
        [FromQuery] bool includeHistory = true,
        [FromQuery] bool includeBenchmark = true,
        [FromQuery] int historyMonths = 6)
    {
        try
        {
            // 1. Validate vehicle exists
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle {VehicleId} not found", id);
                return NotFound(new { message = $"Vehicle {id} not found" });
            }

            // 2. Check authorization - user must be in the vehicle's group
            var userGroupIds = await GetUserGroupIds();
            if (!vehicle.GroupId.HasValue || !userGroupIds.Contains(vehicle.GroupId.Value))
            {
                _logger.LogWarning("User {UserId} attempted to access health score for vehicle {VehicleId} without authorization",
                    GetCurrentUserId(), id);
                return Forbidden(new { message = "You do not have permission to view health score for this vehicle" });
            }

            // 3. Get access token for inter-service calls
            var accessToken = GetAccessToken();

            // 4. Build request
            var request = new HealthScoreRequest
            {
                IncludeHistory = includeHistory,
                IncludeBenchmark = includeBenchmark,
                HistoryMonths = historyMonths
            };

            // 5. Calculate health score
            var healthScore = await _healthScoreService.CalculateHealthScoreAsync(id, request, accessToken);

            _logger.LogInformation(
                "Retrieved health score {Score} ({Category}) for vehicle {VehicleId}",
                healthScore.OverallScore, healthScore.Category, id);

            return Ok(healthScore);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Vehicle {VehicleId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to vehicle health score");
            return Forbidden(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health score for vehicle {VehicleId}", id);
            return StatusCode(500, new { message = "An error occurred while calculating health score" });
        }
    }

    #region Helper Methods

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    private string GetAccessToken()
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            throw new UnauthorizedAccessException("Missing or invalid authorization header");
        }
        return authHeader.Substring("Bearer ".Length).Trim();
    }

    private Domain.Entities.BookingStatus MapBookingStatus(DTOs.BookingStatus status)
    {
        return status switch
        {
            DTOs.BookingStatus.Pending => Domain.Entities.BookingStatus.Pending,
            DTOs.BookingStatus.PendingApproval => Domain.Entities.BookingStatus.PendingApproval,
            DTOs.BookingStatus.Confirmed => Domain.Entities.BookingStatus.Confirmed,
            DTOs.BookingStatus.InProgress => Domain.Entities.BookingStatus.InProgress,
            DTOs.BookingStatus.Completed => Domain.Entities.BookingStatus.Completed,
            DTOs.BookingStatus.Cancelled => Domain.Entities.BookingStatus.Cancelled,
            DTOs.BookingStatus.NoShow => Domain.Entities.BookingStatus.NoShow,
            _ => Domain.Entities.BookingStatus.Pending
        };
    }

    private IActionResult Forbidden(object value)
    {
        return StatusCode(403, value);
    }

    #endregion
}

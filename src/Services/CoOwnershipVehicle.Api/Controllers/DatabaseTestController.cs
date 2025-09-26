using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseTestController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DatabaseTestController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _context.Users
                .Select(u => new 
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.Role,
                    u.KycStatus,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(new { 
                Success = true, 
                Count = users.Count, 
                Users = users 
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { 
                Success = false, 
                Error = ex.Message 
            });
        }
    }

    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        try
        {
            var groups = await _context.OwnershipGroups
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                .Include(g => g.Vehicles)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Description,
                    g.Status,
                    MemberCount = g.Members.Count,
                    VehicleCount = g.Vehicles.Count,
                    Members = g.Members.Select(m => new
                    {
                        m.User.FirstName,
                        m.User.LastName,
                        m.User.Email,
                        m.SharePercentage,
                        m.RoleInGroup
                    })
                })
                .ToListAsync();

            return Ok(new { 
                Success = true, 
                Count = groups.Count, 
                Groups = groups 
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { 
                Success = false, 
                Error = ex.Message 
            });
        }
    }

    [HttpGet("vehicles")]
    public async Task<IActionResult> GetVehicles()
    {
        try
        {
            var vehicles = await _context.Vehicles
                .Include(v => v.Group)
                .Select(v => new
                {
                    v.Id,
                    v.Vin,
                    v.PlateNumber,
                    v.Model,
                    v.Year,
                    v.Color,
                    v.Status,
                    v.Odometer,
                    GroupName = v.Group != null ? v.Group.Name : null
                })
                .ToListAsync();

            return Ok(new { 
                Success = true, 
                Count = vehicles.Count, 
                Vehicles = vehicles 
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { 
                Success = false, 
                Error = ex.Message 
            });
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> DatabaseHealth()
    {
        try
        {
            // Test basic connectivity
            var canConnect = await _context.Database.CanConnectAsync();
            
            if (!canConnect)
            {
                return BadRequest(new { Success = false, Error = "Cannot connect to database" });
            }

            // Get table counts
            var userCount = await _context.Users.CountAsync();
            var groupCount = await _context.OwnershipGroups.CountAsync();
            var vehicleCount = await _context.Vehicles.CountAsync();
            var memberCount = await _context.GroupMembers.CountAsync();

            return Ok(new
            {
                Success = true,
                DatabaseConnected = canConnect,
                TableCounts = new
                {
                    Users = userCount,
                    Groups = groupCount,
                    Vehicles = vehicleCount,
                    GroupMembers = memberCount
                },
                ConnectionString = _context.Database.GetConnectionString()?.Substring(0, 50) + "..."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { 
                Success = false, 
                Error = ex.Message,
                StackTrace = ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace.Length)) 
            });
        }
    }
}

using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System.Text.Json;

namespace CoOwnershipVehicle.Admin.Api.Services;

public class SystemLogsService : ISystemLogsService
{
    private readonly AdminDbContext _context;
    private readonly ILogger<SystemLogsService> _logger;

    public SystemLogsService(AdminDbContext context, ILogger<SystemLogsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SystemLogsResponseDto> GetLogsAsync(SystemLogsRequestDto request)
    {
        // For now, we'll use AuditLogs as the source of logs
        // In production, you'd integrate with a centralized logging solution like Serilog with Seq, ELK, etc.
        
        var query = _context.AuditLogs.AsQueryable();

        // Filter by service (if we track service name in audit logs)
        if (!string.IsNullOrEmpty(request.Service))
        {
            query = query.Where(a => a.Entity.Contains(request.Service, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by level (map to action severity)
        if (!string.IsNullOrEmpty(request.Level))
        {
            var level = request.Level.ToLower();
            if (level == "error" || level == "critical")
            {
                query = query.Where(a => a.Action.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                                       a.Action.Contains("Failed", StringComparison.OrdinalIgnoreCase));
            }
        }

        // Filter by date range
        if (request.FromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= request.ToDate.Value);
        }

        // Search
        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            query = query.Where(a => 
                a.Details.ToLower().Contains(searchTerm) ||
                a.Action.ToLower().Contains(searchTerm) ||
                a.Entity.ToLower().Contains(searchTerm));
        }

        // Sorting
        query = request.SortBy?.ToLower() switch
        {
            "level" or "action" => request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(a => a.Action)
                : query.OrderByDescending(a => a.Action),
            "service" or "entity" => request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(a => a.Entity)
                : query.OrderByDescending(a => a.Entity),
            _ => request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(a => a.Timestamp)
                : query.OrderByDescending(a => a.Timestamp)
        };

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var logs = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new LogEntryDto
            {
                Id = a.Id,
                Service = a.Entity, // Using Entity as service name
                Level = DetermineLogLevel(a.Action),
                Message = a.Action,
                Exception = null,
                Timestamp = a.Timestamp,
                Properties = new Dictionary<string, object>
                {
                    { "Details", a.Details },
                    { "EntityId", a.EntityId.ToString() }
                },
                UserId = a.PerformedBy.ToString(),
                RequestId = null,
                IpAddress = a.IpAddress
            })
            .ToListAsync();

        return new SystemLogsResponseDto
        {
            Logs = logs,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task<byte[]> ExportLogsAsync(SystemLogsRequestDto request, string format = "json")
    {
        var response = await GetLogsAsync(new SystemLogsRequestDto
        {
            Page = 1,
            PageSize = int.MaxValue, // Get all logs
            Service = request.Service,
            Level = request.Level,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Search = request.Search
        });

        return format.ToLower() switch
        {
            "json" => System.Text.Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(response.Logs, new JsonSerializerOptions { WriteIndented = true })),
            "csv" => ExportToCsv(response.Logs),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    private byte[] ExportToCsv(List<LogEntryDto> logs)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Id,Service,Level,Message,Timestamp,UserId,IpAddress");

        foreach (var log in logs)
        {
            csv.AppendLine($"{log.Id},{log.Service},{log.Level},\"{log.Message.Replace("\"", "\"\"")}\",{log.Timestamp:yyyy-MM-dd HH:mm:ss},{log.UserId},{log.IpAddress}");
        }

        return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    }

    private string DetermineLogLevel(string action)
    {
        var actionLower = action.ToLower();
        
        if (actionLower.Contains("error") || actionLower.Contains("failed") || actionLower.Contains("exception"))
            return "Error";
        if (actionLower.Contains("warning") || actionLower.Contains("warn"))
            return "Warning";
        if (actionLower.Contains("created") || actionLower.Contains("updated") || actionLower.Contains("deleted"))
            return "Information";
        
        return "Information";
    }
}


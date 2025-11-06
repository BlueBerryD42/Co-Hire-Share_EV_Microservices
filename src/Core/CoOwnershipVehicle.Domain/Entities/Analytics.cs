using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class AnalyticsSnapshot : BaseEntity
{
    public Guid? GroupId { get; set; }
    
    public Guid? VehicleId { get; set; }
    
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
    
    public AnalyticsPeriod Period { get; set; }
    
    // Usage Statistics
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalDistance { get; set; }
    
    public int TotalBookings { get; set; }
    
    public int TotalUsageHours { get; set; }
    
    public int ActiveUsers { get; set; }
    
    // Financial Statistics
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalRevenue { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalExpenses { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal NetProfit { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal AverageCostPerHour { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal AverageCostPerKm { get; set; }
    
    // Efficiency Metrics
    [Column(TypeName = "decimal(5,4)")]
    public decimal UtilizationRate { get; set; } // % of time vehicle is used
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal MaintenanceEfficiency { get; set; } // Cost per km
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal UserSatisfactionScore { get; set; } // 0-1 scale
    
    // Navigation properties
    public virtual OwnershipGroup? Group { get; set; }
    public virtual Vehicle? Vehicle { get; set; }
}

public class UserAnalytics : BaseEntity
{
    public Guid UserId { get; set; }
    
    public Guid? GroupId { get; set; }
    
    public DateTime PeriodStart { get; set; }
    
    public DateTime PeriodEnd { get; set; }
    
    public AnalyticsPeriod Period { get; set; }
    
    // Usage Statistics
    public int TotalBookings { get; set; }
    
    public int TotalUsageHours { get; set; }
    
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalDistance { get; set; }
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal OwnershipShare { get; set; }
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal UsageShare { get; set; } // Actual usage vs ownership
    
    // Financial Statistics
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPaid { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalOwed { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal NetBalance { get; set; }
    
    // Behavior Metrics
    [Column(TypeName = "decimal(5,4)")]
    public decimal BookingSuccessRate { get; set; } // % of approved bookings
    
    public int Cancellations { get; set; }
    
    public int NoShows { get; set; }
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal PunctualityScore { get; set; } // 0-1 scale
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual OwnershipGroup? Group { get; set; }
}

public class VehicleAnalytics : BaseEntity
{
    public Guid VehicleId { get; set; }
    
    public Guid? GroupId { get; set; }
    
    public DateTime PeriodStart { get; set; }
    
    public DateTime PeriodEnd { get; set; }
    
    public AnalyticsPeriod Period { get; set; }
    
    // Performance Metrics
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalDistance { get; set; }
    
    public int TotalBookings { get; set; }
    
    public int TotalUsageHours { get; set; }
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal UtilizationRate { get; set; } // % of time in use
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal AvailabilityRate { get; set; } // % of time available
    
    // Financial Metrics
    [Column(TypeName = "decimal(18,2)")]
    public decimal Revenue { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal MaintenanceCost { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal OperatingCost { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal NetProfit { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPerKm { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPerHour { get; set; }
    
    // Reliability Metrics
    public int MaintenanceEvents { get; set; }
    
    public int Breakdowns { get; set; }
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal ReliabilityScore { get; set; } // 0-1 scale
    
    // Navigation properties
    public virtual Vehicle Vehicle { get; set; } = null!;
    public virtual OwnershipGroup? Group { get; set; }
}

public class GroupAnalytics : BaseEntity
{
    public Guid GroupId { get; set; }
    
    public DateTime PeriodStart { get; set; }
    
    public DateTime PeriodEnd { get; set; }
    
    public AnalyticsPeriod Period { get; set; }
    
    // Member Statistics
    public int TotalMembers { get; set; }
    
    public int ActiveMembers { get; set; }
    
    public int NewMembers { get; set; }
    
    public int LeftMembers { get; set; }
    
    // Financial Summary
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalRevenue { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalExpenses { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal NetProfit { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal AverageMemberContribution { get; set; }
    
    // Activity Metrics
    public int TotalBookings { get; set; }
    
    public int TotalProposals { get; set; }
    
    public int TotalVotes { get; set; }
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal ParticipationRate { get; set; } // % of members who voted
    
    // Navigation properties
    public virtual OwnershipGroup Group { get; set; } = null!;
}

public enum AnalyticsPeriod
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4
}

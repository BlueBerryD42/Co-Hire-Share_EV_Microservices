using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Vehicle.Api.Data;

public class VehicleDbContext : DbContext
{
    public VehicleDbContext(DbContextOptions<VehicleDbContext> options) : base(options)
    {
    }

    // Vehicle service should only manage its own core entities
    public DbSet<Domain.Entities.Vehicle> Vehicles { get; set; }

    // Maintenance-related entities
    public DbSet<MaintenanceSchedule> MaintenanceSchedules { get; set; }
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }

    // Health score tracking
    public DbSet<VehicleHealthScore> VehicleHealthScores { get; set; }

    // We might need other entities for validation, but without managing them.
    // For simplicity in fixing the issue, we will define a minimal DbContext.
    // The controller logic that depends on other DbSets will need to be adjusted or will fail.

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Vehicle entity configuration
        builder.Entity<Domain.Entities.Vehicle>(entity =>
        {
            entity.ToTable("Vehicles"); // Explicitly name the table
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Vin).IsRequired().HasMaxLength(17);
            entity.Property(e => e.PlateNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<int>();

            // IMPORTANT: Do not create a database-level foreign key constraint to the Group table in another service.
            // The relationship exists at the application level. We just store the ID.
            entity.Ignore(e => e.Group); // Ignore the navigation property

            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.Vin).IsUnique();
            entity.HasIndex(e => e.PlateNumber).IsUnique();
        });

        // MaintenanceSchedule entity configuration
        builder.Entity<MaintenanceSchedule>(entity =>
        {
            entity.ToTable("MaintenanceSchedules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ServiceType).HasConversion<int>().IsRequired();
            entity.Property(e => e.Status).HasConversion<int>().IsRequired();
            entity.Property(e => e.Priority).HasConversion<int>().IsRequired();
            entity.Property(e => e.EstimatedCost).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ServiceProvider).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            // Foreign key to Vehicle
            entity.HasOne(e => e.Vehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.ScheduledDate);
            entity.HasIndex(e => e.Status);
        });

        // MaintenanceRecord entity configuration
        builder.Entity<MaintenanceRecord>(entity =>
        {
            entity.ToTable("MaintenanceRecords");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ServiceType).HasConversion<int>().IsRequired();
            entity.Property(e => e.ActualCost).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.ServiceProvider).IsRequired().HasMaxLength(200);
            entity.Property(e => e.WorkPerformed).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.PartsReplaced).HasMaxLength(1000);

            // Foreign key to Vehicle
            entity.HasOne(e => e.Vehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            // ExpenseId is optional - ignore the navigation property since Expense is in another service
            entity.Ignore(e => e.Expense);

            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.ServiceDate);
            entity.HasIndex(e => e.OdometerReading);
        });

        // VehicleHealthScore entity configuration
        builder.Entity<VehicleHealthScore>(entity =>
        {
            entity.ToTable("VehicleHealthScores");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.OverallScore).HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(e => e.Category).HasConversion<int>().IsRequired();
            entity.Property(e => e.MaintenanceScore).HasColumnType("decimal(5,2)");
            entity.Property(e => e.OdometerAgeScore).HasColumnType("decimal(5,2)");
            entity.Property(e => e.DamageScore).HasColumnType("decimal(5,2)");
            entity.Property(e => e.ServiceFrequencyScore).HasColumnType("decimal(5,2)");
            entity.Property(e => e.VehicleAgeScore).HasColumnType("decimal(5,2)");
            entity.Property(e => e.InspectionScore).HasColumnType("decimal(5,2)");
            entity.Property(e => e.Note).HasMaxLength(500);

            // Foreign key to Vehicle
            entity.HasOne(e => e.Vehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.CalculatedAt);
            entity.HasIndex(e => new { e.VehicleId, e.CalculatedAt });
        });

        // Ignore all other entities that might be discovered transitively
        builder.Ignore<OwnershipGroup>();
        builder.Ignore<GroupMember>();
        builder.Ignore<User>();
        builder.Ignore<Booking>();
        builder.Ignore<KycDocument>();
        builder.Ignore<Expense>();
        builder.Ignore<Invoice>();
        builder.Ignore<Payment>();
        builder.Ignore<Notification>();
        builder.Ignore<AnalyticsSnapshot>();
        builder.Ignore<Document>();
        builder.Ignore<LedgerEntry>();
        builder.Ignore<Proposal>();
        builder.Ignore<AuditLog>();
    }
}
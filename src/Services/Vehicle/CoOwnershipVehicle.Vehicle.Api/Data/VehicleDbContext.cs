using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Vehicle.Api.Data;

public class VehicleDbContext : DbContext
{
    public VehicleDbContext(DbContextOptions<VehicleDbContext> options) : base(options)
    {
    }

    // Vehicle service core entities
    public DbSet<Domain.Entities.Vehicle> Vehicles { get; set; }

    // Maintenance-related entities
    public DbSet<MaintenanceSchedule> MaintenanceSchedules { get; set; }
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }

    // Vehicle health tracking
    public DbSet<VehicleHealthScore> VehicleHealthScores { get; set; }

    // Related entities (read-only access for cross-service validation)
    public DbSet<OwnershipGroup> OwnershipGroups { get; set; } // For group access
    public DbSet<GroupMember> GroupMembers { get; set; } // For access control
    public DbSet<Booking> Bookings { get; set; } // For availability checks
    public DbSet<User> Users { get; set; } // For booking or ownership details
    public DbSet<Expense> Expenses { get; set; } // For maintenance cost linking

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

		// Ignore entities not relevant to Vehicle service
        builder.Ignore<KycDocument>();
        builder.Ignore<Invoice>();
        builder.Ignore<Payment>();
        builder.Ignore<Notification>();
        builder.Ignore<NotificationTemplate>();
        builder.Ignore<AnalyticsSnapshot>();
        builder.Ignore<Document>();
        builder.Ignore<LedgerEntry>();
        builder.Ignore<Proposal>();
        builder.Ignore<AuditLog>();
        builder.Ignore<Vote>();

        // User entity configuration (simplified for Vehicle service)
        builder.Entity<User>(entity =>
        {
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.KycStatus).HasConversion<int>();
            entity.Property(e => e.Role).HasConversion<int>();
            
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone);
            
            // Ignore navigation properties not relevant to Vehicle service
            entity.Ignore(e => e.KycDocuments);
            entity.Ignore(e => e.ExpensesCreated);
            entity.Ignore(e => e.Payments);
            entity.Ignore(e => e.CheckIns);
            entity.Ignore(e => e.Votes);
            entity.Ignore(e => e.AuditLogs);
        });

        // OwnershipGroup entity configuration (simplified for Vehicle service)
        builder.Entity<OwnershipGroup>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<int>();

            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Name);
        });

        // GroupMember entity configuration (simplified for Vehicle service)
        builder.Entity<GroupMember>(entity =>
        {
            entity.Property(e => e.SharePercentage).HasColumnType("decimal(5,4)");
            entity.Property(e => e.RoleInGroup).HasConversion<int>();

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Members)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.GroupMemberships)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
        });

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

    entity.HasOne(e => e.Vehicle)
        .WithMany()
        .HasForeignKey(e => e.VehicleId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasIndex(e => e.VehicleId);
    entity.HasIndex(e => e.ScheduledDate);
    entity.HasIndex(e => e.Status);
});

// ✅ Gộp cấu hình MaintenanceRecord từ cả hai bên
builder.Entity<MaintenanceRecord>(entity =>
{
    entity.ToTable("MaintenanceRecords");
    entity.HasKey(e => e.Id);

    entity.Property(e => e.Status).HasConversion<int>().IsRequired();
    entity.Property(e => e.ServiceType).HasConversion<int>().IsRequired();
    entity.Property(e => e.Priority).HasConversion<int>().IsRequired();
    entity.Property(e => e.ActualCost).HasColumnType("decimal(18,2)").IsRequired();
    entity.Property(e => e.ServiceProvider).IsRequired().HasMaxLength(200);
    entity.Property(e => e.WorkPerformed).IsRequired().HasMaxLength(2000);
    entity.Property(e => e.PartsReplaced).HasMaxLength(1000);

    entity.HasOne(e => e.Vehicle)
        .WithMany(v => v.MaintenanceRecords)
        .HasForeignKey(e => e.VehicleId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(e => e.Group)
        .WithMany()
        .HasForeignKey(e => e.GroupId)
        .OnDelete(DeleteBehavior.SetNull);

    // Expense là optional (read-only)
    entity.Ignore(e => e.Expense);

    entity.HasIndex(e => new { e.VehicleId, e.Status, e.ScheduledDate });
    entity.HasIndex(e => e.ServiceDate);
    entity.HasIndex(e => e.OdometerReading);
});

// Expense minimal configuration (read-only in this service)
builder.Entity<Expense>(entity =>
{
    entity.Property(e => e.ExpenseType).HasConversion<int>();
    entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
});

// Booking entity configuration (simplified for Vehicle service)
builder.Entity<CoOwnershipVehicle.Domain.Entities.Booking>(entity =>
{
    entity.Property(e => e.Status).HasConversion<int>();
    entity.Property(e => e.PriorityScore).HasColumnType("decimal(10,4)");
    entity.Property(e => e.Notes).HasMaxLength(500);
    entity.Property(e => e.PreCheckoutReminderSentAt).HasColumnType("datetime2");
    entity.Property(e => e.FinalCheckoutReminderSentAt).HasColumnType("datetime2");
    entity.Property(e => e.MissedCheckoutReminderSentAt).HasColumnType("datetime2");
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
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
    public DbSet<OwnershipGroup> OwnershipGroups { get; set; }

    // Note: User, OwnershipGroup, GroupMember, Booking, Expense entities are NOT included here.
    // Vehicle service only stores foreign key IDs (Guid) and fetches related data via HTTP calls or events.

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Explicitly ignore Identity entities - Vehicle service doesn't use Identity
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>();

		// Ignore entities not relevant to Vehicle service
        builder.Ignore<KycDocument>();
        builder.Ignore<Invoice>();
        builder.Ignore<Payment>();
        builder.Ignore<Notification>();
        builder.Ignore<NotificationTemplate>();
        builder.Ignore<AnalyticsSnapshot>();
        builder.Ignore<Document>();
        builder.Ignore<DocumentTag>();
        builder.Ignore<DocumentTagMapping>();
        builder.Ignore<DocumentSignature>();
        builder.Ignore<DocumentDownload>();
        builder.Ignore<DocumentVersion>();
        builder.Ignore<DocumentTemplate>();
        builder.Ignore<DocumentShare>();
        builder.Ignore<DocumentShareAccess>();
        builder.Ignore<SigningCertificate>();
        builder.Ignore<SignatureReminder>();
        builder.Ignore<SavedDocumentSearch>();
        builder.Ignore<LedgerEntry>();
        builder.Ignore<Proposal>();
        builder.Ignore<AuditLog>();
        builder.Ignore<Vote>();

        // Ignore cross-service entities - Vehicle service only stores foreign key IDs
        builder.Ignore<User>();
        builder.Ignore<GroupMember>();
        builder.Ignore<Booking>();
        builder.Ignore<Expense>();

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
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.SubmittedAt).IsRequired(false);
            entity.Property(e => e.ReviewedBy).IsRequired(false);
            entity.Property(e => e.ReviewedAt).IsRequired(false);

            // IMPORTANT: Do not create a database-level foreign key constraint to the Group table in another service.
            // The relationship exists at the application level. We just store the ID.
            entity.Ignore(e => e.Group); // Ignore the navigation property
            // Removed: entity.HasOne<OwnershipGroup>().WithMany().HasForeignKey(e => e.GroupId);

            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.SubmittedAt);
            entity.HasIndex(e => e.ReviewedBy);
            entity.HasIndex(e => e.Vin).IsUnique();
            entity.HasIndex(e => e.PlateNumber).IsUnique();
        });

        builder.Entity<OwnershipGroup>(entity =>
        {
            entity.ToTable("OwnershipGroups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
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

//  Gộp cấu hình MaintenanceRecord từ cả hai bên
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

    // GroupId is stored but no FK constraint (Group is in another service)
    entity.Ignore(e => e.Group);
    entity.Ignore(e => e.Expense);

    entity.HasIndex(e => new { e.VehicleId, e.Status, e.ScheduledDate });
    entity.HasIndex(e => e.ScheduledDate);
    entity.HasIndex(e => e.OdometerReading);
});

// Note: Expense and Booking entities are not configured here - they belong to other services.
// Vehicle service only stores foreign key IDs when needed.


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
        builder.Ignore<KycDocument>();
        builder.Ignore<Invoice>();
        builder.Ignore<Payment>();
        builder.Ignore<Notification>();
        builder.Ignore<AnalyticsSnapshot>();
        builder.Ignore<Document>();
        builder.Ignore<DocumentTag>();
        builder.Ignore<DocumentTagMapping>();
        builder.Ignore<DocumentSignature>();
        builder.Ignore<DocumentDownload>();
        builder.Ignore<DocumentVersion>();
        builder.Ignore<DocumentTemplate>();
        builder.Ignore<DocumentShare>();
        builder.Ignore<DocumentShareAccess>();
        builder.Ignore<SigningCertificate>();
        builder.Ignore<SignatureReminder>();
        builder.Ignore<SavedDocumentSearch>();
        builder.Ignore<LedgerEntry>();
        builder.Ignore<Proposal>();
        builder.Ignore<AuditLog>();
    }
}

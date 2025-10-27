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
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    // Booking service entities - only what it actually needs
    public DbSet<CoOwnershipVehicle.Domain.Entities.Booking> Bookings { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; } // For booking details
    public DbSet<User> Users { get; set; } // For user details
    public DbSet<OwnershipGroup> OwnershipGroups { get; set; } // For group access
    public DbSet<GroupMember> GroupMembers { get; set; } // For priority calculation
    public DbSet<CheckIn> CheckIns { get; set; } // Vehicle handover check-ins
    public DbSet<CheckInPhoto> CheckInPhotos { get; set; } // Supporting media for check-ins
    public DbSet<DamageReport> DamageReports { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore entities not relevant to Booking service
        builder.Ignore<KycDocument>();
        builder.Ignore<Expense>();
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

        // User entity configuration (simplified for Booking service)
        builder.Entity<User>(entity =>
        {
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.KycStatus).HasConversion<int>();
            entity.Property(e => e.Role).HasConversion<int>();
            
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone);
            
            // Ignore navigation properties not relevant to Booking service
            entity.Ignore(e => e.KycDocuments);
            entity.Ignore(e => e.ExpensesCreated);
            entity.Ignore(e => e.Payments);
            entity.Ignore(e => e.CheckIns);
            entity.Ignore(e => e.Votes);
            entity.Ignore(e => e.AuditLogs);
        });

        // OwnershipGroup entity configuration (simplified for Booking service)
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

        // GroupMember entity configuration (simplified for Booking service)
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

        // Vehicle entity configuration (simplified for Booking service)
        builder.Entity<Vehicle>(entity =>
        {
            entity.Property(e => e.Vin).IsRequired().HasMaxLength(17);
            entity.Property(e => e.PlateNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<int>();

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Vehicles)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Vin).IsUnique();
            entity.HasIndex(e => e.PlateNumber).IsUnique();
        });

        // Booking entity configuration
        builder.Entity<CoOwnershipVehicle.Domain.Entities.Booking>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.PriorityScore).HasColumnType("decimal(10,4)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.RequiresDamageReview).HasDefaultValue(false);

            entity.HasOne(e => e.Vehicle)
                  .WithMany(v => v.Bookings)
                  .HasForeignKey(e => e.VehicleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Bookings)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Bookings)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.VehicleId, e.StartAt, e.EndAt });
            entity.HasIndex(e => e.StartAt);
        });

        // Check-in entity configuration
        builder.Entity<CheckIn>(entity =>
        {
            entity.ToTable("CheckIn");

            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Odometer);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.SignatureReference).HasMaxLength(500);
            entity.Property(e => e.SignatureDevice).HasMaxLength(200);
            entity.Property(e => e.SignatureDeviceId).HasMaxLength(100);
            entity.Property(e => e.SignatureIpAddress).HasMaxLength(45);
            entity.Property(e => e.SignatureCapturedAt).HasColumnType("datetime2");
            entity.Property(e => e.SignatureHash).HasMaxLength(128);
            entity.Property(e => e.SignatureCertificateUrl).HasMaxLength(500);
            entity.Property(e => e.SignatureMetadataJson).HasMaxLength(2000);
            entity.Property(e => e.CheckInTime).HasColumnType("datetime2");

            entity.HasOne(e => e.Booking)
                  .WithMany(b => b.CheckIns)
                  .HasForeignKey(e => e.BookingId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Vehicle)
                  .WithMany(v => v.CheckIns)
                  .HasForeignKey(e => e.VehicleId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<CheckInPhoto>(entity =>
        {
            entity.ToTable("CheckInPhoto");

            entity.Property(e => e.PhotoUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.StoragePath).HasMaxLength(1000);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(1000);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(e => e.CheckIn)
                  .WithMany(c => c.Photos)
                  .HasForeignKey(e => e.CheckInId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DamageReport>(entity =>
        {
            entity.ToTable("DamageReport");

            entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.EstimatedCost).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.PhotoIdsJson).HasMaxLength(2000);
            entity.Property(e => e.Severity).HasConversion<int>();
            entity.Property(e => e.Location).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();

            entity.HasOne(e => e.CheckIn)
                .WithMany()
                .HasForeignKey(e => e.CheckInId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CheckInId);
            entity.HasIndex(e => e.BookingId);
            entity.HasIndex(e => e.VehicleId);
        });

        // Configure automatic timestamp updates
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .Property<DateTime>("UpdatedAt")
                    .HasDefaultValueSql("GETUTCDATE()");
            }
        }
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(e => e.CreatedAt).CurrentValue = DateTime.UtcNow;
                entry.Property(e => e.UpdatedAt).CurrentValue = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.UpdatedAt).CurrentValue = DateTime.UtcNow;
            }
        }
    }
}

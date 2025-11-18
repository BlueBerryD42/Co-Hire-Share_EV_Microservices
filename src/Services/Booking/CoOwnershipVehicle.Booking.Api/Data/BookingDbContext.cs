using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    public DbSet<CoOwnershipVehicle.Domain.Entities.Booking> Bookings { get; set; } = null!;
    public DbSet<CheckIn> CheckIns { get; set; } = null!;
    public DbSet<CheckInPhoto> CheckInPhotos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Explicitly ignore Identity entities - Booking service doesn't use Identity
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>();

        builder.Ignore<User>();
        builder.Ignore<OwnershipGroup>();
        builder.Ignore<GroupMember>();
        builder.Ignore<Vehicle>();
        builder.Ignore<KycDocument>();
        builder.Ignore<Expense>();
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
        builder.Ignore<MaintenanceSchedule>();
        builder.Ignore<MaintenanceRecord>();
        builder.Ignore<GroupFund>();
        builder.Ignore<FundTransaction>();
        builder.Ignore<LateReturnFee>();
        builder.Ignore<DamageReport>();
        builder.Ignore<RecurringBooking>();
        builder.Ignore<BookingTemplate>();

        builder.Entity<CoOwnershipVehicle.Domain.Entities.Booking>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.PriorityScore).HasColumnType("decimal(10,4)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.VehicleStatus).HasConversion<int>().HasDefaultValue(VehicleStatus.Available);
            entity.Property(e => e.DistanceKm).HasColumnType("decimal(8,2)");
            entity.Property(e => e.TripFeeAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);

            entity.Ignore(e => e.Vehicle);
            entity.Ignore(e => e.Group);
            entity.Ignore(e => e.User);

            entity.HasIndex(e => new { e.VehicleId, e.StartAt, e.EndAt });
            entity.HasIndex(e => e.StartAt);
        });

        builder.Entity<CheckIn>(entity =>
        {
            entity.ToTable("CheckIn");
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.SignatureReference).HasMaxLength(500);

            entity.HasOne(e => e.Booking)
                .WithMany(b => b.CheckIns)
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Ignore(e => e.User);
            entity.Ignore(e => e.Vehicle);
            entity.Ignore(e => e.LateReturnFee);
        });

        builder.Entity<CheckInPhoto>(entity =>
        {
            entity.ToTable("CheckInPhoto");
            entity.Property(e => e.PhotoUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Type).HasConversion<int>();

            entity.HasOne(e => e.CheckIn)
                .WithMany(c => c.Photos)
                .HasForeignKey(e => e.CheckInId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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

using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Booking.Api.Entities;

namespace CoOwnershipVehicle.Booking.Api.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    // Booking service entities - only what it actually needs
    public DbSet<CoOwnershipVehicle.Domain.Entities.Booking> Bookings { get; set; }
    
    // Note: User, Vehicle, OwnershipGroup, GroupMember entities are NOT included here.
    // Booking service only stores foreign key IDs (Guid) and fetches related data via HTTP calls or events.
    public DbSet<MaintenanceBlock> MaintenanceBlocks { get; set; } // For preventing bookings during maintenance
    public DbSet<CheckIn> CheckIns { get; set; } // Vehicle handover check-ins
    public DbSet<CheckInPhoto> CheckInPhotos { get; set; } // Supporting media for check-ins
    public DbSet<LateReturnFee> LateReturnFees { get; set; } // Late fee records
    public DbSet<DamageReport> DamageReports { get; set; }
    public DbSet<BookingNotificationPreference> NotificationPreferences { get; set; }
    public DbSet<RecurringBooking> RecurringBookings { get; set; }
    public DbSet<BookingTemplate> BookingTemplates { get; set; } // Added BookingTemplate

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

        // Ignore cross-service entities - Booking service only stores foreign key IDs
        builder.Ignore<User>();
        builder.Ignore<OwnershipGroup>();
        builder.Ignore<GroupMember>();
        builder.Ignore<Vehicle>();

        // Booking entity configuration
        builder.Entity<CoOwnershipVehicle.Domain.Entities.Booking>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.PriorityScore).HasColumnType("decimal(10,4)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.RequiresDamageReview).HasDefaultValue(false);
            entity.Property(e => e.PreCheckoutReminderSentAt).HasColumnType("datetime2");
            entity.Property(e => e.FinalCheckoutReminderSentAt).HasColumnType("datetime2");
            entity.Property(e => e.MissedCheckoutReminderSentAt).HasColumnType("datetime2");
            entity.Property(e => e.RecurringBookingId);
            entity.Property(e => e.BookingTemplateId); // Added BookingTemplateId

            // VehicleId, GroupId, UserId are stored but no FK constraints (entities are in other services)
            entity.Ignore(e => e.Vehicle);
            entity.Ignore(e => e.Group);
            entity.Ignore(e => e.User);

            entity.HasOne(e => e.RecurringBooking)
                  .WithMany(rb => rb.GeneratedBookings)
                  .HasForeignKey(e => e.RecurringBookingId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.BookingTemplate) // Added navigation property
                .WithMany() // BookingTemplate entity in this workspace doesn't expose GeneratedBookings; use unlinked collection
                .HasForeignKey(e => e.BookingTemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.VehicleId, e.StartAt, e.EndAt });
            entity.HasIndex(e => e.StartAt);
        });
// MaintenanceBlock entity configuration (from HEAD)
builder.Entity<MaintenanceBlock>(entity =>
{
    entity.Property(e => e.ServiceType).HasConversion<int>();
    entity.Property(e => e.Status).HasConversion<int>();
    entity.Property(e => e.Priority).HasConversion<int>();
    entity.Property(e => e.Notes).HasMaxLength(1000);

    // Ignore navigation property - no FK constraint in microservices
    entity.Ignore(e => e.Vehicle);

    entity.HasIndex(e => e.MaintenanceScheduleId).IsUnique();
    entity.HasIndex(e => new { e.VehicleId, e.StartTime, e.EndTime });
    entity.HasIndex(e => e.Status);
});

// RecurringBooking entity configuration (from main)
builder.Entity<RecurringBooking>(entity =>
{
    entity.Property(e => e.Pattern).HasConversion<int>();
    entity.Property(e => e.Status).HasConversion<int>();
    entity.Property(e => e.Interval).HasDefaultValue(1);
    entity.Property(e => e.DaysOfWeekMask);
    entity.Property(e => e.StartTime).HasColumnType("time");
    entity.Property(e => e.EndTime).HasColumnType("time");
    entity.Property(e => e.RecurrenceStartDate).HasColumnType("date");
    entity.Property(e => e.RecurrenceEndDate).HasColumnType("date");
    entity.Property(e => e.Notes).HasMaxLength(500);
    entity.Property(e => e.Purpose).HasMaxLength(200);
    entity.Property(e => e.CancellationReason).HasMaxLength(200);
    entity.Property(e => e.TimeZoneId).HasMaxLength(100);

    // VehicleId, GroupId, UserId are stored but no FK constraints (entities are in other services)
    entity.Ignore(e => e.Vehicle);
    entity.Ignore(e => e.Group);
    entity.Ignore(e => e.User);

    entity.HasIndex(e => e.Status);
    entity.HasIndex(e => e.VehicleId);
    entity.HasIndex(e => e.UserId);
});

// BookingTemplate entity configuration
builder.Entity<BookingTemplate>(entity =>
{
    entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
    entity.Property(e => e.Duration).HasColumnType("time");
    entity.Property(e => e.PreferredStartTime).HasColumnType("time");
    entity.Property(e => e.Purpose).HasMaxLength(500);
    entity.Property(e => e.Notes).HasMaxLength(1000);
    entity.Property(e => e.Priority).HasConversion<int>();
    entity.Property(e => e.UsageCount).HasDefaultValue(0);

    // UserId, VehicleId are stored but no FK constraints (entities are in other services)
    entity.Ignore(e => e.User);
    entity.Ignore(e => e.Vehicle);

    entity.HasIndex(e => e.UserId);
    entity.HasIndex(e => e.VehicleId);
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
    entity.Property(e => e.IsLateReturn).HasDefaultValue(false);
    entity.Property(e => e.LateReturnMinutes);
    entity.Property(e => e.LateFeeAmount).HasColumnType("decimal(18,2)");

    entity.HasOne(e => e.Booking)
          .WithMany(b => b.CheckIns)
          .HasForeignKey(e => e.BookingId)
          .OnDelete(DeleteBehavior.Cascade);

    // UserId, VehicleId are stored but no FK constraints (entities are in other services)
    entity.Ignore(e => e.User);
    entity.Ignore(e => e.Vehicle);

    entity.HasOne(e => e.LateReturnFee)
          .WithOne(l => l.CheckIn)
          .HasForeignKey<LateReturnFee>(l => l.CheckInId)
          .OnDelete(DeleteBehavior.Cascade);
});

// CheckInPhoto entity configuration
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

// LateReturnFee entity configuration
builder.Entity<LateReturnFee>(entity =>
{
    entity.ToTable("LateReturnFee");

    entity.Property(e => e.FeeAmount).HasColumnType("decimal(18,2)");
    entity.Property(e => e.OriginalFeeAmount).HasColumnType("decimal(18,2)");
    entity.Property(e => e.CalculationMethod).HasMaxLength(200);
    entity.Property(e => e.WaivedReason).HasMaxLength(500);
    entity.Property(e => e.Status).HasConversion<int>();

    entity.HasOne(e => e.Booking)
          .WithMany(b => b.LateReturnFees)
          .HasForeignKey(e => e.BookingId)
          .OnDelete(DeleteBehavior.NoAction);

    // UserId, VehicleId, GroupId are stored but no FK constraints (entities are in other services)
    entity.Ignore(e => e.User);
    entity.Ignore(e => e.Vehicle);
    // GroupId is stored but no navigation property

    entity.HasIndex(e => e.BookingId);
    entity.HasIndex(e => e.UserId);
    entity.HasIndex(e => e.Status);
});

// DamageReport entity configuration
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

// BookingNotificationPreference entity configuration
builder.Entity<BookingNotificationPreference>(entity =>
{
    entity.ToTable("BookingNotificationPreference");

    entity.HasKey(e => e.UserId);
    entity.Property(e => e.EnableReminders).HasDefaultValue(true);
    entity.Property(e => e.EnableEmail).HasDefaultValue(true);
    entity.Property(e => e.EnableSms).HasDefaultValue(true);
    entity.Property(e => e.PreferredTimeZoneId).HasMaxLength(100);
    entity.Property(e => e.Notes).HasMaxLength(250);
    entity.Property(e => e.UpdatedAt).HasColumnType("datetime2");
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

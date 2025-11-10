using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Analytics.Api.Data;

public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options)
    {
    }

    // Analytics service entities - only what it actually needs
    public DbSet<CoOwnershipVehicle.Domain.Entities.AnalyticsSnapshot> AnalyticsSnapshots { get; set; }
    public DbSet<CoOwnershipVehicle.Domain.Entities.UserAnalytics> UserAnalytics { get; set; }
    public DbSet<CoOwnershipVehicle.Analytics.Api.Data.Entities.FairnessTrend> FairnessTrends { get; set; }
    public DbSet<CoOwnershipVehicle.Analytics.Api.Data.Entities.BookingSuggestionFeedback> BookingSuggestionFeedback { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore all entities that are not part of the Analytics service
        builder.Ignore<User>();
        builder.Ignore<KycDocument>();
        builder.Ignore<OwnershipGroup>();
        builder.Ignore<GroupMember>();
        builder.Ignore<Vehicle>();
        builder.Ignore<Expense>();
        builder.Ignore<Invoice>();
        builder.Ignore<Payment>();
        builder.Ignore<Booking>();
        builder.Ignore<CheckIn>();
        builder.Ignore<Notification>();
        builder.Ignore<NotificationTemplate>();
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
        builder.Ignore<Vote>();
        builder.Ignore<AuditLog>();

        // AnalyticsSnapshot entity configuration
        builder.Entity<CoOwnershipVehicle.Domain.Entities.AnalyticsSnapshot>(entity =>
        {
            entity.Property(e => e.SnapshotDate).IsRequired();
            entity.Property(e => e.Period).HasConversion<int>();
            entity.Property(e => e.TotalDistance).HasColumnType("decimal(10,2)");
            entity.Property(e => e.TotalRevenue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalExpenses).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetProfit).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AverageCostPerHour).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AverageCostPerKm).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UtilizationRate).HasColumnType("decimal(5,4)");
            entity.Property(e => e.MaintenanceEfficiency).HasColumnType("decimal(5,4)");
            entity.Property(e => e.UserSatisfactionScore).HasColumnType("decimal(5,4)");

            entity.HasIndex(e => e.SnapshotDate);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.Period);
        });

        // Minimal configuration for UserAnalytics used by AI fairness computation
        builder.Entity<CoOwnershipVehicle.Domain.Entities.UserAnalytics>(entity =>
        {
            entity.Property(e => e.Period).HasConversion<int>();
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.PeriodStart);
        });

        // FairnessTrend entity configuration
        builder.Entity<CoOwnershipVehicle.Analytics.Api.Data.Entities.FairnessTrend>(entity =>
        {
            entity.HasIndex(e => new { e.GroupId, e.PeriodStart, e.PeriodEnd }).IsUnique();
        });

        // BookingSuggestionFeedback entity configuration
        builder.Entity<CoOwnershipVehicle.Analytics.Api.Data.Entities.BookingSuggestionFeedback>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.GroupId, e.SuggestedStart });
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

using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Notification.Api.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    // Notification service entities - only what it actually needs
    public DbSet<CoOwnershipVehicle.Domain.Entities.Notification> Notifications { get; set; }
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore entities not relevant to Notification service
        builder.Ignore<KycDocument>();
        builder.Ignore<Expense>();
        builder.Ignore<Invoice>();
        builder.Ignore<Payment>();
        builder.Ignore<Booking>();
        builder.Ignore<CheckIn>();
        builder.Ignore<AnalyticsSnapshot>();
        builder.Ignore<Document>();
        builder.Ignore<LedgerEntry>();
        builder.Ignore<Proposal>();
        builder.Ignore<AuditLog>();
        builder.Ignore<Vote>();
        builder.Ignore<User>();
        builder.Ignore<OwnershipGroup>();
        builder.Ignore<GroupMember>();
        builder.Ignore<Vehicle>();

        // Notification entity configuration
        builder.Entity<CoOwnershipVehicle.Domain.Entities.Notification>(entity =>
        {
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Priority).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.ActionText).HasMaxLength(100);

            // In microservices architecture, we don't create foreign key relationships
            // to entities from other services. We just store the IDs as simple properties.
            // The relationships are maintained at the application level, not database level.
            
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ScheduledFor);
        });

        // NotificationTemplate entity configuration
        builder.Entity<NotificationTemplate>(entity =>
        {
            entity.Property(e => e.TemplateKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TitleTemplate).IsRequired().HasMaxLength(200);
            entity.Property(e => e.MessageTemplate).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Priority).HasConversion<int>();
            entity.Property(e => e.ActionUrlTemplate).HasMaxLength(500);
            entity.Property(e => e.ActionText).HasMaxLength(100);

            entity.HasIndex(e => e.TemplateKey).IsUnique();
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

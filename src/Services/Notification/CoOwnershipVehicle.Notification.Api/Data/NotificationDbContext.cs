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
        // CRITICAL: Configure Notification entity FIRST, before base.OnModelCreating()
        // This prevents base.OnModelCreating() from applying relationship conventions
        builder.Entity<CoOwnershipVehicle.Domain.Entities.Notification>(entity =>
        {
            // Ignore navigation properties IMMEDIATELY to prevent relationship creation
            entity.Ignore(e => e.User);
            entity.Ignore(e => e.Group);
            
            // Configure properties
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Priority).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.ActionText).HasMaxLength(100);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.GroupId).IsRequired(false);
            
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ScheduledFor);
        });

        // Now call base - but Notification entity is already configured, so relationships won't be created
        base.OnModelCreating(builder);

        // Explicitly ignore Identity entities - Notification service doesn't use Identity
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>();
        builder.Ignore<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>();

        // Ignore entities not relevant to Notification service
        builder.Ignore<KycDocument>();
        builder.Ignore<Expense>();
        builder.Ignore<Invoice>();
        builder.Ignore<Payment>();
        builder.Ignore<Booking>();
        builder.Ignore<CheckIn>();
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
        builder.Ignore<User>();
        builder.Ignore<OwnershipGroup>();
        builder.Ignore<GroupMember>();
        builder.Ignore<Vehicle>();

        // Re-configure Notification entity to ensure navigation properties stay ignored
        // This overrides any relationships that base.OnModelCreating() might have created
        builder.Entity<CoOwnershipVehicle.Domain.Entities.Notification>(entity =>
        {
            // Force ignore navigation properties again after base.OnModelCreating()
            entity.Ignore(e => e.User);
            entity.Ignore(e => e.Group);
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

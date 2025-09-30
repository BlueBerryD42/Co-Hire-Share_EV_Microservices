using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.User.Api.Data;

public class UserDbContext : IdentityDbContext<CoOwnershipVehicle.Domain.Entities.User, IdentityRole<Guid>, Guid>
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
    {
    }

    // User service entities - only what it actually needs
    public DbSet<KycDocument> KycDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore all entities that are not part of the User service
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
        builder.Ignore<AnalyticsSnapshot>();
        builder.Ignore<Document>();
        builder.Ignore<LedgerEntry>();
        builder.Ignore<Proposal>();
        builder.Ignore<Vote>();
        builder.Ignore<AuditLog>();

        // Configure Identity tables with custom names
        builder.Entity<CoOwnershipVehicle.Domain.Entities.User>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        // User entity configuration
        builder.Entity<CoOwnershipVehicle.Domain.Entities.User>(entity =>
        {
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.KycStatus).HasConversion<int>();
            entity.Property(e => e.Role).HasConversion<int>();
            
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone);

            // Ignore navigation properties not relevant to User service
            entity.Ignore(e => e.GroupMemberships);
            entity.Ignore(e => e.ExpensesCreated);
            entity.Ignore(e => e.Payments);
            entity.Ignore(e => e.Bookings);
            entity.Ignore(e => e.CheckIns);
            entity.Ignore(e => e.Votes);
            entity.Ignore(e => e.AuditLogs);
        });

        // KycDocument entity configuration
        builder.Entity<KycDocument>(entity =>
        {
            entity.Property(e => e.DocumentType).HasConversion<int>();
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.StorageUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.ReviewNotes).HasMaxLength(1000);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.KycDocuments)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Reviewer)
                  .WithMany()
                  .HasForeignKey(e => e.ReviewedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
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

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Auth.Api.Data;

public class AuthDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    // Only include entities that the Auth service actually needs
    // Auth service only needs User and Identity-related tables

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Identity tables with custom names
        builder.Entity<User>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");

        // User entity configuration
        builder.Entity<User>(entity =>
        {
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.KycStatus).HasConversion<int>();
            entity.Property(e => e.Role).HasConversion<int>();
            
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone);
            
            // Ignore navigation properties that aren't needed in Auth service
            entity.Ignore(e => e.KycDocuments);
            entity.Ignore(e => e.GroupMemberships);
            entity.Ignore(e => e.ExpensesCreated);
            entity.Ignore(e => e.Payments);
            entity.Ignore(e => e.Bookings);
        });

        // Ignore entities that aren't needed in Auth service
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
        builder.Ignore<AnalyticsSnapshot>();
        builder.Ignore<Document>();
        builder.Ignore<LedgerEntry>();
        builder.Ignore<Proposal>();
        builder.Ignore<AuditLog>();
        builder.Ignore<Vote>();
        builder.Ignore<CheckInPhoto>();
        builder.Ignore<UserAnalytics>();
        builder.Ignore<VehicleAnalytics>();
        builder.Ignore<GroupAnalytics>();
        builder.Ignore<GroupFund>();
        builder.Ignore<FundTransaction>();

        // Ignore new document management entities
        builder.Ignore<DocumentTemplate>();
        builder.Ignore<DocumentShare>();
        builder.Ignore<DocumentShareAccess>();
        builder.Ignore<DocumentTag>();
        builder.Ignore<DocumentTagMapping>();
        builder.Ignore<SavedDocumentSearch>();
        builder.Ignore<DocumentSignature>();
        builder.Ignore<DocumentDownload>();
        builder.Ignore<SigningCertificate>();
        builder.Ignore<DocumentVersion>();
        builder.Ignore<SignatureReminder>();

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

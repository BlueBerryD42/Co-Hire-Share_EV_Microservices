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

        // User entity configuration for Auth service
        // NOTE: Auth service should ONLY store authentication-related data
        // ALL profile fields (FirstName, LastName, Phone, Role, KycStatus, etc.) are stored in User service database
        // Auth DB stores ONLY Identity fields: Email, UserName, PasswordHash, SecurityStamp, etc.
        builder.Entity<User>(entity =>
        {
            // Authentication fields (REQUIRED - these are stored in Auth DB)
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.NormalizedEmail);
            entity.HasIndex(e => e.NormalizedUserName);
            
            // Remove ALL profile fields from Auth DB - they belong ONLY in User service database
            entity.Ignore(e => e.FirstName);
            entity.Ignore(e => e.LastName);
            entity.Ignore(e => e.Phone);
            entity.Ignore(e => e.Address);
            entity.Ignore(e => e.City);
            entity.Ignore(e => e.Country);
            entity.Ignore(e => e.PostalCode);
            entity.Ignore(e => e.DateOfBirth);
            entity.Ignore(e => e.KycStatus);
            entity.Ignore(e => e.Role);
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.UpdatedAt);
            
            // Ignore navigation properties that aren't needed in Auth service
            entity.Ignore(e => e.KycDocuments);
            entity.Ignore(e => e.GroupMemberships);
            entity.Ignore(e => e.ExpensesCreated);
            entity.Ignore(e => e.Payments);
            entity.Ignore(e => e.Bookings);
            entity.Ignore(e => e.CheckIns);
            entity.Ignore(e => e.Votes);
            entity.Ignore(e => e.AuditLogs);
            entity.Ignore(e => e.RecurringBookings);
            entity.Ignore(e => e.InitiatedFundTransactions);
            entity.Ignore(e => e.ApprovedFundTransactions);
        });

        // Ignore entities that aren't needed in Auth service
        builder.Ignore<KycDocument>();
        builder.Ignore<OwnershipGroup>();
        builder.Ignore<GroupMember>();
        builder.Ignore<Vehicle>();
        builder.Ignore<Expense>();
        builder.Ignore<Invoice>();
        builder.Ignore<Payment>();
        builder.Ignore<GroupFund>();
        builder.Ignore<FundTransaction>();
        builder.Ignore<Booking>();
        builder.Ignore<RecurringBooking>();
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

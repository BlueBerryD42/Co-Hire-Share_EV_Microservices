using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Admin.Api.Data;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
    {
    }

    // Admin service entities - only Admin-specific entities
    // All other entities (User, Group, Vehicle, Booking, Payment, etc.) are accessed via HTTP calls to their respective services
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Dispute> Disputes { get; set; }
    public DbSet<DisputeComment> DisputeComments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore entities that aren't needed in Admin service
        modelBuilder.Ignore<AnalyticsSnapshot>();
        // Ignore all entities from other services - they are accessed via HTTP
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<OwnershipGroup>();
        modelBuilder.Ignore<Vehicle>();
        modelBuilder.Ignore<Booking>();
        modelBuilder.Ignore<Payment>();
        modelBuilder.Ignore<Invoice>();
        modelBuilder.Ignore<Expense>();
        modelBuilder.Ignore<Notification>();
        modelBuilder.Ignore<Proposal>();
        modelBuilder.Ignore<GroupMember>();
        modelBuilder.Ignore<CheckIn>();
        modelBuilder.Ignore<Document>();
        modelBuilder.Ignore<KycDocument>();
        modelBuilder.Ignore<LedgerEntry>();
        modelBuilder.Ignore<GroupFund>();
        modelBuilder.Ignore<FundTransaction>();
        modelBuilder.Ignore<Vote>();

        // Configure AuditLog entity
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Entity).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure relationships for Admin-specific entities only
        // Note: Disputes reference Group and User, but these are foreign keys only (no navigation properties needed)
        // The actual Group and User entities are in other services

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.Timestamp);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => new { a.Entity, a.EntityId });

        // Configure Dispute entity
        // Note: Disputes reference Group and User entities from other services via foreign keys only
        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Subject).IsRequired().HasMaxLength(200);
            entity.Property(d => d.Description).IsRequired().HasMaxLength(2000);
            entity.Property(d => d.Category).HasConversion<int>();
            entity.Property(d => d.Priority).HasConversion<int>();
            entity.Property(d => d.Status).HasConversion<int>();
            entity.Property(d => d.Resolution).HasMaxLength(2000);
            entity.Property(d => d.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(d => d.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Foreign key relationships (no navigation properties to external entities)
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.ReportedBy);
            entity.HasIndex(e => e.AssignedTo);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Priority);
            entity.HasIndex(e => e.Category);
        });

        // Configure DisputeComment entity
        modelBuilder.Entity<DisputeComment>(entity =>
        {
            entity.HasKey(dc => dc.Id);
            entity.Property(dc => dc.Comment).IsRequired().HasMaxLength(2000);
            entity.Property(dc => dc.IsInternal).IsRequired();
            entity.Property(dc => dc.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(dc => dc.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(dc => dc.Dispute)
                .WithMany(d => d.Comments)
                .HasForeignKey(dc => dc.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Foreign key to User (no navigation property to external entity)
            entity.HasIndex(e => e.DisputeId);
            entity.HasIndex(e => e.CommentedBy);
        });

        // Configure automatic timestamp updates
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
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

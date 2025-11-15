using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Admin.Api.Data;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
    {
    }

    // Admin service entities - only what it actually needs for dashboard
    public DbSet<User> Users { get; set; }
    public DbSet<OwnershipGroup> OwnershipGroups { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Proposal> Proposals { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<CheckIn> CheckIns { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<KycDocument> KycDocuments { get; set; }
    public DbSet<LedgerEntry> LedgerEntries { get; set; }
    public DbSet<GroupFund> GroupFunds { get; set; }
    public DbSet<FundTransaction> FundTransactions { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<Dispute> Disputes { get; set; }
    public DbSet<DisputeComment> DisputeComments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore entities that aren't needed in Admin service
        modelBuilder.Ignore<AnalyticsSnapshot>();

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.PostalCode).HasMaxLength(20);
            entity.Property(e => e.KycStatus).HasConversion<int>();
            entity.Property(e => e.Role).HasConversion<int>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone);
        });

        // Configure OwnershipGroup entity
        modelBuilder.Entity<OwnershipGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Vehicle entity
        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Vin).IsRequired().HasMaxLength(17);
            entity.Property(e => e.PlateNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Booking entity
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Purpose).HasMaxLength(200);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Priority).HasConversion<int>();
            entity.Property(e => e.PriorityScore).HasColumnType("decimal(10,4)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure Payment entity
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Method).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.TransactionReference).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

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

        // Configure relationships
        modelBuilder.Entity<OwnershipGroup>()
            .HasOne(g => g.Creator)
            .WithMany()
            .HasForeignKey(g => g.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.Group)
            .WithMany(g => g.Vehicles)
            .HasForeignKey(v => v.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Vehicle)
            .WithMany(v => v.Bookings)
            .HasForeignKey(b => b.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Group)
            .WithMany(g => g.Bookings)
            .HasForeignKey(b => b.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Payer)
            .WithMany(u => u.Payments)
            .HasForeignKey(p => p.PayerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.PerformedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure indexes for better performance
        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.Vin)
            .IsUnique();

        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.PlateNumber)
            .IsUnique();

        modelBuilder.Entity<Booking>()
            .HasIndex(b => new { b.VehicleId, b.StartAt, b.EndAt });

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.TransactionReference);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.Timestamp);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => new { a.Entity, a.EntityId });

        // Configure GroupFund entity
        modelBuilder.Entity<GroupFund>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ReserveBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Group)
                  .WithOne(g => g.Fund)
                  .HasForeignKey<GroupFund>(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.GroupId).IsUnique();
        });

        // Configure FundTransaction entity
        modelBuilder.Entity<FundTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Reference).HasMaxLength(200);
            entity.Property(e => e.TransactionDate).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.FundTransactions)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Initiator)
                  .WithMany(u => u.InitiatedFundTransactions)
                  .HasForeignKey(e => e.InitiatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Approver)
                  .WithMany(u => u.ApprovedFundTransactions)
                  .HasForeignKey(e => e.ApprovedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.InitiatedBy);
            entity.HasIndex(e => e.ApprovedBy);
            entity.HasIndex(e => new { e.GroupId, e.TransactionDate });
        });

        // Configure KycDocument entity relationships
        modelBuilder.Entity<KycDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.StorageUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ReviewNotes).HasMaxLength(1000);
            entity.Property(e => e.DocumentType).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure User relationship (document owner)
            entity.HasOne(d => d.User)
                  .WithMany(u => u.KycDocuments)
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Configure Reviewer relationship
            entity.HasOne(d => d.Reviewer)
                  .WithMany()
                  .HasForeignKey(d => d.ReviewedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
        });

        // Configure Vote entity to avoid cascade path conflicts
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Weight).HasColumnType("decimal(5,4)");
            entity.Property(e => e.Choice).HasConversion<int>();
            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure Proposal relationship
            entity.HasOne(v => v.Proposal)
                  .WithMany(p => p.Votes)
                  .HasForeignKey(v => v.ProposalId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Configure Voter relationship - use Restrict to avoid cascade path conflicts
            entity.HasOne(v => v.Voter)
                  .WithMany()
                  .HasForeignKey(v => v.VoterId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ProposalId);
            entity.HasIndex(e => e.VoterId);
        });

        // Configure Dispute entity
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

            entity.HasOne(d => d.Group)
                .WithMany()
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Reporter)
                .WithMany()
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.AssignedStaff)
                .WithMany()
                .HasForeignKey(d => d.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Resolver)
                .WithMany()
                .HasForeignKey(d => d.ResolvedBy)
                .OnDelete(DeleteBehavior.SetNull);

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

            entity.HasOne(dc => dc.Commenter)
                .WithMany()
                .HasForeignKey(dc => dc.CommentedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.DisputeId);
            entity.HasIndex(e => e.CommentedBy);
        });

        // Configure Invoice entity to avoid cascade path conflicts
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure Expense relationship
            entity.HasOne(i => i.Expense)
                  .WithMany()
                  .HasForeignKey(i => i.ExpenseId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Configure Payer relationship - use Restrict to avoid cascade path conflicts
            entity.HasOne(i => i.Payer)
                  .WithMany()
                  .HasForeignKey(i => i.PayerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ExpenseId);
            entity.HasIndex(e => e.PayerId);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
        });

        // Configure Expense entity to avoid cascade path conflicts
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.ExpenseType).HasConversion<int>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure Group relationship
            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Configure Vehicle relationship
            entity.HasOne(e => e.Vehicle)
                  .WithMany()
                  .HasForeignKey(e => e.VehicleId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Configure Creator relationship - use Restrict to avoid cascade path conflicts
            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.CreatedBy);
        });

        // Configure DocumentTagMapping entity (composite key for many-to-many relationship)
        modelBuilder.Entity<DocumentTagMapping>(entity =>
        {
            entity.HasKey(e => new { e.DocumentId, e.TagId });

            entity.HasOne(e => e.Document)
                  .WithMany(d => d.TagMappings)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Tag)
                  .WithMany(t => t.DocumentMappings)
                  .HasForeignKey(e => e.TagId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TaggerUser)
                  .WithMany()
                  .HasForeignKey(e => e.TaggedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.TagId);
            entity.HasIndex(e => e.TaggedAt);
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

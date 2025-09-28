using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Data;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // DbSets for entities
    public DbSet<OwnershipGroup> OwnershipGroups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<DocumentSignature> DocumentSignatures { get; set; }
    public DbSet<CheckIn> CheckIns { get; set; }
    public DbSet<CheckInPhoto> CheckInPhotos { get; set; }
    public DbSet<Proposal> Proposals { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<LedgerEntry> LedgerEntries { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<KycDocument> KycDocuments { get; set; }
    
    // Notification entities
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; }
    
    // Analytics entities
    public DbSet<AnalyticsSnapshot> AnalyticsSnapshots { get; set; }
    public DbSet<UserAnalytics> UserAnalytics { get; set; }
    public DbSet<VehicleAnalytics> VehicleAnalytics { get; set; }
    public DbSet<GroupAnalytics> GroupAnalytics { get; set; }

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
        });

        // OwnershipGroup entity configuration
        builder.Entity<OwnershipGroup>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status).HasConversion<int>();

            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Name);
        });

        // GroupMember entity configuration
        builder.Entity<GroupMember>(entity =>
        {
            entity.Property(e => e.SharePercentage).HasColumnType("decimal(5,4)");
            entity.Property(e => e.RoleInGroup).HasConversion<int>();

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Members)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.GroupMemberships)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
        });

        // Vehicle entity configuration
        builder.Entity<Vehicle>(entity =>
        {
            entity.Property(e => e.Vin).IsRequired().HasMaxLength(17);
            entity.Property(e => e.PlateNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<int>();

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Vehicles)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Vin).IsUnique();
            entity.HasIndex(e => e.PlateNumber).IsUnique();
        });

        // Booking entity configuration
        builder.Entity<Booking>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.PriorityScore).HasColumnType("decimal(10,4)");
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(e => e.Vehicle)
                  .WithMany(v => v.Bookings)
                  .HasForeignKey(e => e.VehicleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Bookings)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Bookings)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.VehicleId, e.StartAt, e.EndAt });
            entity.HasIndex(e => e.StartAt);
        });

        // Expense entity configuration
        builder.Entity<Expense>(entity =>
        {
            entity.Property(e => e.ExpenseType).HasConversion<int>();
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Expenses)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Vehicle)
                  .WithMany(v => v.Expenses)
                  .HasForeignKey(e => e.VehicleId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Creator)
                  .WithMany(u => u.ExpensesCreated)
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.DateIncurred);
        });

        // Invoice entity configuration
        builder.Entity<Invoice>(entity =>
        {
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).HasConversion<int>();

            entity.HasOne(e => e.Expense)
                  .WithMany(ex => ex.Invoices)
                  .HasForeignKey(e => e.ExpenseId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Payer)
                  .WithMany()
                  .HasForeignKey(e => e.PayerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.HasIndex(e => e.DueDate);
        });

        // Payment entity configuration
        builder.Entity<Payment>(entity =>
        {
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Method).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.TransactionReference).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.Payments)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Payer)
                  .WithMany(u => u.Payments)
                  .HasForeignKey(e => e.PayerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.TransactionReference);
        });

        // Document entity configuration
        builder.Entity<Document>(entity =>
        {
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.StorageKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SignatureStatus).HasConversion<int>();
            entity.Property(e => e.Description).HasMaxLength(1000);

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Documents)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // DocumentSignature entity configuration
        builder.Entity<DocumentSignature>(entity =>
        {
            entity.Property(e => e.SignatureReference).IsRequired().HasMaxLength(500);

            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Signatures)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Signer)
                  .WithMany()
                  .HasForeignKey(e => e.SignerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.DocumentId, e.SignerId }).IsUnique();
        });

        // CheckIn entity configuration
        builder.Entity<CheckIn>(entity =>
        {
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.SignatureReference).HasMaxLength(500);

            entity.HasOne(e => e.Booking)
                  .WithMany(b => b.CheckIns)
                  .HasForeignKey(e => e.BookingId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.CheckIns)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // CheckInPhoto entity configuration
        builder.Entity<CheckInPhoto>(entity =>
        {
            entity.Property(e => e.PhotoUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Type).HasConversion<int>();

            entity.HasOne(e => e.CheckIn)
                  .WithMany(c => c.Photos)
                  .HasForeignKey(e => e.CheckInId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Proposal entity configuration
        builder.Entity<Proposal>(entity =>
        {
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RequiredMajority).HasColumnType("decimal(5,4)");

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Proposals)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Vote entity configuration
        builder.Entity<Vote>(entity =>
        {
            entity.Property(e => e.Weight).HasColumnType("decimal(5,4)");
            entity.Property(e => e.Choice).HasConversion<int>();
            entity.Property(e => e.Comment).HasMaxLength(500);

            entity.HasOne(e => e.Proposal)
                  .WithMany(p => p.Votes)
                  .HasForeignKey(e => e.ProposalId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Voter)
                  .WithMany(u => u.Votes)
                  .HasForeignKey(e => e.VoterId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ProposalId, e.VoterId }).IsUnique();
        });

        // LedgerEntry entity configuration
        builder.Entity<LedgerEntry>(entity =>
        {
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.Property(e => e.RelatedEntityType).HasMaxLength(100);

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.LedgerEntries)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CreatedAt);
        });

        // AuditLog entity configuration
        builder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.Entity).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.AuditLogs)
                  .HasForeignKey(e => e.PerformedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.Entity, e.EntityId });
            entity.HasIndex(e => e.Timestamp);
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

        // Notification entity configuration
        builder.Entity<Notification>(entity =>
        {
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Priority).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.ActionText).HasMaxLength(100);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.SetNull);

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

        // AnalyticsSnapshot entity configuration
        builder.Entity<AnalyticsSnapshot>(entity =>
        {
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

            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Vehicle)
                  .WithMany()
                  .HasForeignKey(e => e.VehicleId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.SnapshotDate);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.Period);
        });

        // UserAnalytics entity configuration
        builder.Entity<UserAnalytics>(entity =>
        {
            entity.Property(e => e.Period).HasConversion<int>();
            entity.Property(e => e.TotalDistance).HasColumnType("decimal(10,2)");
            entity.Property(e => e.OwnershipShare).HasColumnType("decimal(5,4)");
            entity.Property(e => e.UsageShare).HasColumnType("decimal(5,4)");
            entity.Property(e => e.TotalPaid).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalOwed).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BookingSuccessRate).HasColumnType("decimal(5,4)");
            entity.Property(e => e.PunctualityScore).HasColumnType("decimal(5,4)");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.PeriodStart);
        });

        // VehicleAnalytics entity configuration
        builder.Entity<VehicleAnalytics>(entity =>
        {
            entity.Property(e => e.Period).HasConversion<int>();
            entity.Property(e => e.TotalDistance).HasColumnType("decimal(10,2)");
            entity.Property(e => e.UtilizationRate).HasColumnType("decimal(5,4)");
            entity.Property(e => e.AvailabilityRate).HasColumnType("decimal(5,4)");
            entity.Property(e => e.Revenue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaintenanceCost).HasColumnType("decimal(18,2)");
            entity.Property(e => e.OperatingCost).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetProfit).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CostPerKm).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CostPerHour).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ReliabilityScore).HasColumnType("decimal(5,4)");

            entity.HasOne(e => e.Vehicle)
                  .WithMany()
                  .HasForeignKey(e => e.VehicleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.PeriodStart);
        });

        // GroupAnalytics entity configuration
        builder.Entity<GroupAnalytics>(entity =>
        {
            entity.Property(e => e.Period).HasConversion<int>();
            entity.Property(e => e.TotalRevenue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalExpenses).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetProfit).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AverageMemberContribution).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ParticipationRate).HasColumnType("decimal(5,4)");

            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.PeriodStart);
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

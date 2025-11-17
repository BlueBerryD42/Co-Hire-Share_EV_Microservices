using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Group.Api.Data;

public class GroupDbContext : DbContext
{
    public GroupDbContext(DbContextOptions<GroupDbContext> options) : base(options)
    {
    }

    // Group service entities - only what it actually needs
    public DbSet<OwnershipGroup> OwnershipGroups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<User> Users { get; set; } // For member details
    public DbSet<Vehicle> Vehicles { get; set; } // For group vehicles
    public DbSet<Document> Documents { get; set; } // For document management
    public DbSet<DocumentSignature> DocumentSignatures { get; set; } // For document signatures
    public DbSet<DocumentDownload> DocumentDownloads { get; set; } // For download tracking
    public DbSet<SigningCertificate> SigningCertificates { get; set; } // For signing certificates
    public DbSet<DocumentVersion> DocumentVersions { get; set; } // For version control
    public DbSet<SignatureReminder> SignatureReminders { get; set; } // For signature reminders

    // New document features
    public DbSet<DocumentTemplate> DocumentTemplates { get; set; } // For pre-built templates
    public DbSet<DocumentShare> DocumentShares { get; set; } // For external sharing
    public DbSet<DocumentShareAccess> DocumentShareAccesses { get; set; } // For share access logs
    public DbSet<DocumentTag> DocumentTags { get; set; } // For document tagging
    public DbSet<DocumentTagMapping> DocumentTagMappings { get; set; } // For document-tag relationships
    public DbSet<SavedDocumentSearch> SavedDocumentSearches { get; set; } // For saved searches

    // Voting system entities
    public DbSet<Proposal> Proposals { get; set; }
    public DbSet<Vote> Votes { get; set; }

    // Fund management entities
    public DbSet<GroupFund> GroupFunds { get; set; }
    public DbSet<FundTransaction> FundTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore entities not relevant to Group service
        builder.Ignore<KycDocument>();
        builder.Ignore<Expense>();
        builder.Ignore<Invoice>();
        builder.Ignore<Payment>();
        builder.Ignore<Booking>();
        builder.Ignore<CheckIn>();
        builder.Ignore<CheckInPhoto>();
        builder.Ignore<Notification>();
        builder.Ignore<NotificationTemplate>();
        builder.Ignore<AnalyticsSnapshot>();
        builder.Ignore<UserAnalytics>();
        builder.Ignore<VehicleAnalytics>();
        builder.Ignore<GroupAnalytics>();
        builder.Ignore<LedgerEntry>();
        builder.Ignore<AuditLog>();

        // User entity configuration (simplified for Group service)
        builder.Entity<User>(entity =>
        {
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.KycStatus).HasConversion<int>();
            entity.Property(e => e.Role).HasConversion<int>();
            
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone);
            
            // Ignore navigation properties not relevant to Group service
            entity.Ignore(e => e.KycDocuments);
            entity.Ignore(e => e.ExpensesCreated);
            entity.Ignore(e => e.Payments);
            entity.Ignore(e => e.Bookings);
            entity.Ignore(e => e.CheckIns);
            entity.Ignore(e => e.AuditLogs);
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

        // Vehicle entity configuration (simplified for Group service)
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

            // Ignore navigation properties not relevant to Group service
            entity.Ignore(e => e.Bookings);
            entity.Ignore(e => e.Expenses);
        });

        // Document entity configuration
        builder.Entity<Document>(entity =>
        {
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.StorageKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SignatureStatus).HasConversion<int>();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.FileHash).HasMaxLength(64);
            entity.Property(e => e.Author).HasMaxLength(200);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Documents)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Add indexes
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.StorageKey).IsUnique();
            entity.HasIndex(e => new { e.GroupId, e.FileHash });
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UploadedBy);
            entity.HasIndex(e => e.IsDeleted);
            entity.HasIndex(e => new { e.IsDeleted, e.DeletedAt });

            // Add query filter for soft delete (global filter)
            entity.HasQueryFilter(e => !e.IsDeleted);
            
            // Explicit table name to match migration
            entity.ToTable("Documents");
        });

        // DocumentSignature entity configuration
        builder.Entity<DocumentSignature>(entity =>
        {
            entity.ToTable("DocumentSignatures"); // Match the migration table name (renamed from DocumentSignature to DocumentSignatures)
            
            entity.Property(e => e.SignatureReference).HasMaxLength(500);
            entity.Property(e => e.SignatureMetadata).HasMaxLength(2000);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.SigningToken).HasMaxLength(500);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.SigningMode).HasConversion<int>();

            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Signatures)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Signer)
                  .WithMany()
                  .HasForeignKey(e => e.SignerId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Add indexes
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.SignerId);
            entity.HasIndex(e => e.SigningToken).IsUnique();
            entity.HasIndex(e => new { e.DocumentId, e.SignerId });
            entity.HasIndex(e => new { e.DocumentId, e.SignatureOrder });
            entity.HasIndex(e => e.TokenExpiresAt);
            entity.HasIndex(e => e.DueDate);
        });

        // DocumentDownload entity configuration
        builder.Entity<DocumentDownload>(entity =>
        {
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.DownloadedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Downloads)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Add indexes
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.DownloadedAt);
            entity.HasIndex(e => new { e.DocumentId, e.UserId });
        });

        // SigningCertificate entity configuration
        builder.Entity<SigningCertificate>(entity =>
        {
            entity.Property(e => e.CertificateId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DocumentHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SignersJson).HasMaxLength(4000);
            entity.Property(e => e.RevocationReason).HasMaxLength(500);

            entity.HasOne(e => e.Document)
                  .WithMany()
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Add indexes
            entity.HasIndex(e => e.CertificateId).IsUnique();
            entity.HasIndex(e => e.DocumentId).IsUnique(); // One certificate per document
            entity.HasIndex(e => e.GeneratedAt);
            entity.HasIndex(e => new { e.DocumentHash, e.CertificateId });
        });

        // DocumentVersion entity configuration
        builder.Entity<DocumentVersion>(entity =>
        {
            entity.Property(e => e.StorageKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FileHash).HasMaxLength(64);
            entity.Property(e => e.ChangeDescription).HasMaxLength(1000);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Versions)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Uploader)
                  .WithMany()
                  .HasForeignKey(e => e.UploadedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            // Add indexes
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.StorageKey).IsUnique();
            entity.HasIndex(e => new { e.DocumentId, e.VersionNumber }).IsUnique();
            entity.HasIndex(e => new { e.DocumentId, e.IsCurrent });
            entity.HasIndex(e => e.UploadedAt);
        });

        // SignatureReminder entity configuration
        builder.Entity<SignatureReminder>(entity =>
        {
            entity.Property(e => e.ReminderType).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.SentAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.DocumentSignature)
                  .WithMany()
                  .HasForeignKey(e => e.DocumentSignatureId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Add indexes
            entity.HasIndex(e => e.DocumentSignatureId);
            entity.HasIndex(e => e.SentAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.DocumentSignatureId, e.ReminderType });
        });

        // DocumentTemplate entity configuration
        builder.Entity<DocumentTemplate>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).HasConversion<int>();
            entity.Property(e => e.TemplateContent).IsRequired();
            entity.Property(e => e.VariablesJson).HasDefaultValue("[]");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Version).HasDefaultValue(1);
            entity.Property(e => e.PreviewImageUrl).HasMaxLength(500);

            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.Category, e.IsActive });
        });

        // DocumentShare entity configuration
        builder.Entity<DocumentShare>(entity =>
        {
            entity.Property(e => e.ShareToken).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SharedWith).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RecipientEmail).HasMaxLength(200);
            entity.Property(e => e.Permissions).HasConversion<int>();
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.AccessCount).HasDefaultValue(0);
            entity.Property(e => e.IsRevoked).HasDefaultValue(false);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);

            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Shares)
                  .HasForeignKey(e => e.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sharer)
                  .WithMany()
                  .HasForeignKey(e => e.SharedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ShareToken).IsUnique();
            entity.HasIndex(e => e.DocumentId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.IsRevoked);
            entity.HasIndex(e => new { e.ShareToken, e.IsRevoked, e.ExpiresAt });
        });

        // DocumentShareAccess entity configuration
        builder.Entity<DocumentShareAccess>(entity =>
        {
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.Action).HasConversion<int>();
            entity.Property(e => e.FailureReason).HasMaxLength(500);

            entity.HasOne(e => e.DocumentShare)
                  .WithMany(s => s.AccessLog)
                  .HasForeignKey(e => e.DocumentShareId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.DocumentShareId);
            entity.HasIndex(e => e.AccessedAt);
            entity.HasIndex(e => e.Action);
        });

        // DocumentTag entity configuration
        builder.Entity<DocumentTag>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.UsageCount).HasDefaultValue(0);

            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => new { e.Name, e.GroupId }).IsUnique();
        });

        // DocumentTagMapping entity configuration
        builder.Entity<DocumentTagMapping>(entity =>
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

        // SavedDocumentSearch entity configuration
        builder.Entity<SavedDocumentSearch>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SearchCriteriaJson).HasDefaultValue("{}");
            entity.Property(e => e.UsageCount).HasDefaultValue(0);
            entity.Property(e => e.IsDefault).HasDefaultValue(false);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => new { e.UserId, e.IsDefault });
        });

        // Update Document entity to include template relationship
        builder.Entity<Document>(entity =>
        {
            entity.HasOne(e => e.Template)
                  .WithMany(t => t.GeneratedDocuments)
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.TemplateId);
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

            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.CreatedBy);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.GroupId, e.Status });
            entity.HasIndex(e => e.VotingEndDate);
        });

        // Vote entity configuration
        builder.Entity<Vote>(entity =>
        {
            entity.Property(e => e.Weight).HasColumnType("decimal(5,4)");
            entity.Property(e => e.Choice).HasConversion<int>();
            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.VotedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Proposal)
                  .WithMany(p => p.Votes)
                  .HasForeignKey(e => e.ProposalId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Voter)
                  .WithMany(u => u.Votes)
                  .HasForeignKey(e => e.VoterId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ProposalId);
            entity.HasIndex(e => e.VoterId);
            entity.HasIndex(e => new { e.ProposalId, e.VoterId }).IsUnique(); // One vote per member per proposal
            entity.HasIndex(e => e.VotedAt);
        });

        // GroupFund entity configuration
        builder.Entity<GroupFund>(entity =>
        {
            entity.Property(e => e.TotalBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ReserveBalance).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.Group)
                  .WithOne(g => g.Fund)
                  .HasForeignKey<GroupFund>(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.GroupId).IsUnique(); // One fund per group
            entity.HasIndex(e => e.LastUpdated);
        });

        // FundTransaction entity configuration
        builder.Entity<FundTransaction>(entity =>
        {
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Reference).HasMaxLength(200);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
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
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TransactionDate);
            entity.HasIndex(e => new { e.GroupId, e.Status });
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

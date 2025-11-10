using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.IntegrationTests.TestFixtures;
using FluentAssertions;

namespace CoOwnershipVehicle.IntegrationTests.AdminWorkflows;

public class AdminWorkflowTests : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "AdminWorkflow")]
    public async Task AdminKycReviewWorkflow_ReviewKycApproveUser_ShouldSucceed()
    {
        // Setup: Create user with pending KYC
        var user = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Pending);
        var kycDoc = TestDataBuilder.CreateKycDocument(user.Id);
        DbContext.KycDocuments.Add(kycDoc);
        await DbContext.SaveChangesAsync();

        kycDoc.Status.Should().Be(KycDocumentStatus.Pending);

        // Admin reviews KYC
        var admin = await CreateAndSaveUserAsync(UserRole.SystemAdmin, KycStatus.Approved);
        
        // Simulate admin review process
        kycDoc.Status = KycDocumentStatus.Approved;
        kycDoc.ReviewedAt = DateTime.UtcNow;
        kycDoc.ReviewedBy = admin.Id;
        user.KycStatus = KycStatus.Approved;
        await DbContext.SaveChangesAsync();

        // Verify
        var updatedDoc = await DbContext.KycDocuments.FindAsync(kycDoc.Id);
        updatedDoc!.Status.Should().Be(KycDocumentStatus.Approved);
        updatedDoc.ReviewedBy.Should().Be(admin.Id);
        
        var updatedUser = await DbContext.Users.FindAsync(user.Id);
        updatedUser!.KycStatus.Should().Be(KycStatus.Approved);
    }

    [Fact]
    [Trait("Category", "AdminWorkflow")]
    public async Task AdminDashboardWorkflow_ViewDashboardDrillIntoGroup_ShouldSucceed()
    {
        // Setup: Create multiple groups with data
        var admin = await CreateAndSaveUserAsync(UserRole.SystemAdmin, KycStatus.Approved);
        
        var group1 = await CreateAndSaveGroupAsync(
            admin.Id,
            new List<Guid> { admin.Id, (await CreateAndSaveUserAsync()).Id }
        );
        var group2 = await CreateAndSaveGroupAsync(
            admin.Id,
            new List<Guid> { admin.Id, (await CreateAndSaveUserAsync()).Id }
        );

        var vehicle1 = await CreateAndSaveVehicleAsync(group1.Id);
        var vehicle2 = await CreateAndSaveVehicleAsync(group2.Id);

        // Create some bookings
        var booking1 = TestDataBuilder.CreateTestBooking(vehicle1.Id, group1.Id, admin.Id);
        var booking2 = TestDataBuilder.CreateTestBooking(vehicle2.Id, group2.Id, admin.Id);
        DbContext.Bookings.AddRange(booking1, booking2);
        await DbContext.SaveChangesAsync();

        // Admin views dashboard (aggregate metrics)
        var totalGroups = await DbContext.OwnershipGroups.CountAsync();
        var totalVehicles = await DbContext.Vehicles.CountAsync();
        var totalUsers = await DbContext.Users.CountAsync();
        var totalBookings = await DbContext.Bookings.CountAsync();

        totalGroups.Should().BeGreaterOrEqualTo(2);
        totalVehicles.Should().BeGreaterOrEqualTo(2);
        totalUsers.Should().BeGreaterOrEqualTo(2);
        totalBookings.Should().BeGreaterOrEqualTo(2);

        // Admin drills into specific group
        var groupDetails = await DbContext.OwnershipGroups
            .Include(g => g.Members)
            .Include(g => g.Vehicles)
            .FirstOrDefaultAsync(g => g.Id == group1.Id);

        groupDetails.Should().NotBeNull();
        groupDetails!.Members.Should().HaveCount(2);
        groupDetails.Vehicles.Should().HaveCount(1);
    }

    [Fact]
    [Trait("Category", "AdminWorkflow")]
    public async Task AdminDisputeResolutionWorkflow_ResolveDispute_ShouldSucceed()
    {
        // Setup: Create dispute
        var reporter = await CreateAndSaveUserAsync();
        var group = await CreateAndSaveGroupAsync(
            reporter.Id,
            new List<Guid> { reporter.Id, (await CreateAndSaveUserAsync()).Id }
        );

        var dispute = TestDataBuilder.CreateDispute(group.Id, reporter.Id, DisputeCategory.VehicleDamage);
        DbContext.Disputes.Add(dispute);
        await DbContext.SaveChangesAsync();

        dispute.Status.Should().Be(DisputeStatus.Open);

        // Admin assigns dispute
        var admin = await CreateAndSaveUserAsync(UserRole.SystemAdmin, KycStatus.Approved);
        dispute.AssignedTo = admin.Id;
        dispute.Status = DisputeStatus.UnderReview;
        dispute.UpdatedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();

        var updatedDispute = await DbContext.Disputes.FindAsync(dispute.Id);
        updatedDispute!.Status.Should().Be(DisputeStatus.UnderReview);
        updatedDispute.AssignedTo.Should().Be(admin.Id);

        // Admin adds comment
        var comment = new DisputeComment
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            CommentedBy = admin.Id,
            Comment = "Reviewing vehicle damage photos",
            IsInternal = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.DisputeComments.Add(comment);
        await DbContext.SaveChangesAsync();

        // Admin resolves dispute
        dispute.Status = DisputeStatus.Resolved;
        dispute.Resolution = "Vehicle damage resolved through insurance claim";
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.UpdatedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();

        var resolvedDispute = await DbContext.Disputes.FindAsync(dispute.Id);
        resolvedDispute!.Status.Should().Be(DisputeStatus.Resolved);
        resolvedDispute.Resolution.Should().NotBeNull();
        resolvedDispute.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "AdminWorkflow")]
    public async Task AdminReportsWorkflow_RunReportsExportData_ShouldSucceed()
    {
        // Setup: Create data for reports
        var admin = await CreateAndSaveUserAsync(UserRole.SystemAdmin, KycStatus.Approved);
        
        var users = new List<User>();
        for (int i = 0; i < 10; i++)
        {
            users.Add(await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved));
        }

        var groups = new List<OwnershipGroup>();
        for (int i = 0; i < 5; i++)
        {
            var memberIds = users.Skip(i * 2).Take(2).Select(u => u.Id).ToList();
            if (memberIds.Count == 2)
            {
                groups.Add(await CreateAndSaveGroupAsync(memberIds[0], memberIds));
            }
        }

        // Run user report
        var userReport = await DbContext.Users
            .Where(u => u.KycStatus == KycStatus.Approved)
            .CountAsync();
        
        userReport.Should().BeGreaterOrEqualTo(10);

        // Run group report
        var groupReport = await DbContext.OwnershipGroups
            .Include(g => g.Members)
            .Select(g => new
            {
                GroupId = g.Id,
                GroupName = g.Name,
                MemberCount = g.Members.Count,
                Status = g.Status
            })
            .ToListAsync();

        groupReport.Should().HaveCountGreaterOrEqualTo(5);
        groupReport.All(g => g.MemberCount > 0).Should().BeTrue();

        // Run booking report
        var vehicles = await DbContext.Vehicles
            .Where(v => v.GroupId != null)
            .ToListAsync();

        var bookings = new List<Booking>();
        foreach (var vehicle in vehicles.Take(3))
        {
            var booking = TestDataBuilder.CreateTestBooking(
                vehicle.Id,
                vehicle.GroupId!.Value,
                users.First().Id
            );
            bookings.Add(booking);
        }
        DbContext.Bookings.AddRange(bookings);
        await DbContext.SaveChangesAsync();

        var bookingReport = await DbContext.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
            .CountAsync();

        bookingReport.Should().BeGreaterOrEqualTo(3);

        // Export data (simulated - in real scenario would generate CSV/Excel)
        var exportData = new
        {
            Users = userReport,
            Groups = groups.Count,
            Bookings = bookingReport,
            ExportDate = DateTime.UtcNow
        };

        exportData.Users.Should().BeGreaterThan(0);
        exportData.Groups.Should().BeGreaterThan(0);
        exportData.Bookings.Should().BeGreaterThan(0);
    }
}






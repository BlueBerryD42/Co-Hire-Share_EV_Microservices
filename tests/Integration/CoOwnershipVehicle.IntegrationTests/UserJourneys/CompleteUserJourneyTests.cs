using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.IntegrationTests.TestFixtures;
using FluentAssertions;

namespace CoOwnershipVehicle.IntegrationTests.UserJourneys;

public class CompleteUserJourneyTests : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "UserJourney")]
    public async Task CompleteUserJourney_RegistrationToPayment_ShouldSucceed()
    {
        // 1. Registration → KYC
        var user = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Pending);
        
        // Upload KYC document
        var kycDoc = TestDataBuilder.CreateKycDocument(user.Id);
        DbContext.KycDocuments.Add(kycDoc);
        await DbContext.SaveChangesAsync();

        // Verify KYC document created
        var savedDoc = await DbContext.KycDocuments.FindAsync(kycDoc.Id);
        savedDoc.Should().NotBeNull();
        savedDoc!.Status.Should().Be(KycDocumentStatus.Pending);

        // Admin approves KYC (simulated)
        user.KycStatus = KycStatus.Approved;
        kycDoc.Status = KycDocumentStatus.Approved;
        await DbContext.SaveChangesAsync();

        user.KycStatus.Should().Be(KycStatus.Approved);

        // 2. Join Group (or create group)
        var groupCreator = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member1 = user;
        var group = await CreateAndSaveGroupAsync(groupCreator.Id, new List<Guid> { groupCreator.Id, member1.Id });

        var groupMembers = await DbContext.GroupMembers
            .Where(m => m.GroupId == group.Id)
            .ToListAsync();
        groupMembers.Should().HaveCount(2);
        groupMembers.Should().Contain(m => m.UserId == user.Id);

        // 3. Add Vehicle to Group
        var vehicle = await CreateAndSaveVehicleAsync(group.Id);
        vehicle.GroupId.Should().Be(group.Id);
        vehicle.Status.Should().Be(VehicleStatus.Available);

        // 4. Create Booking
        var booking = TestDataBuilder.CreateTestBooking(vehicle.Id, group.Id, user.Id, BookingStatus.Confirmed);
        DbContext.Bookings.Add(booking);
        await DbContext.SaveChangesAsync();

        booking.Status.Should().Be(BookingStatus.Confirmed);

        // 5. Check-out
        var checkOut = TestDataBuilder.CreateCheckIn(booking.Id, user.Id, CheckInType.CheckOut, 10000);
        DbContext.CheckIns.Add(checkOut);
        vehicle.Status = VehicleStatus.InUse;
        await DbContext.SaveChangesAsync();

        checkOut.Type.Should().Be(CheckInType.CheckOut);
        var updatedVehicle = await DbContext.Vehicles.FindAsync(vehicle.Id);
        updatedVehicle!.Status.Should().Be(VehicleStatus.InUse);

        // 6. Check-in
        var checkIn = TestDataBuilder.CreateCheckIn(booking.Id, user.Id, CheckInType.CheckIn, 10100);
        DbContext.CheckIns.Add(checkIn);
        booking.Status = BookingStatus.Completed;
        updatedVehicle.Status = VehicleStatus.Available;
        await DbContext.SaveChangesAsync();

        checkIn.Type.Should().Be(CheckInType.CheckIn);
        var completedBooking = await DbContext.Bookings.FindAsync(booking.Id);
        completedBooking!.Status.Should().Be(BookingStatus.Completed);
        var finalVehicle = await DbContext.Vehicles.FindAsync(vehicle.Id);
        finalVehicle!.Status.Should().Be(VehicleStatus.Available);

        // Calculate distance traveled
        var distance = checkIn.Odometer - checkOut.Odometer;
        distance.Should().Be(100);

        // 7. Create Expense (maintenance, fuel, etc.)
        var expense = TestDataBuilder.CreateTestExpense(group.Id, vehicle.Id, 150.00m, ExpenseType.Maintenance);
        DbContext.Expenses.Add(expense);
        await DbContext.SaveChangesAsync();

        expense.Amount.Should().Be(150.00m);

        // 8. Create Invoice for expense split (one invoice per member)
        var invoices = groupMembers.Select(m => new Invoice
        {
            Id = Guid.NewGuid(),
            ExpenseId = expense.Id,
            PayerId = m.UserId,
            Amount = expense.Amount * m.SharePercentage,
            InvoiceNumber = $"INV-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
            Status = InvoiceStatus.Pending,
            DueDate = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        DbContext.Invoices.AddRange(invoices);
        await DbContext.SaveChangesAsync();

        var userInvoice = invoices.First(i => i.PayerId == user.Id);
        userInvoice.Amount.Should().Be(75.00m); // 50% share

        // 9. Process Payment
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = userInvoice.Id,
            PayerId = user.Id,
            Amount = userInvoice.Amount,
            Method = PaymentMethod.CreditCard,
            Status = PaymentStatus.Completed,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.Payments.Add(payment);
        userInvoice.Status = InvoiceStatus.Paid;
        await DbContext.SaveChangesAsync();

        payment.Status.Should().Be(PaymentStatus.Completed);
        var paidInvoice = await DbContext.Invoices.FindAsync(userInvoice.Id);
        paidInvoice!.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    [Trait("Category", "UserJourney")]
    public async Task GroupManagementJourney_CreateGroupAddMembersAddVehicle_ShouldSucceed()
    {
        // 1. Create Group
        var creator = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member1 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member2 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        
        var group = await CreateAndSaveGroupAsync(
            creator.Id, 
            new List<Guid> { creator.Id, member1.Id, member2.Id }
        );

        group.Status.Should().Be(GroupStatus.Active);
        var members = await DbContext.GroupMembers.Where(m => m.GroupId == group.Id).ToListAsync();
        members.Should().HaveCount(3);

        // 2. Add Vehicle
        var vehicle1 = await CreateAndSaveVehicleAsync(group.Id);
        var vehicle2 = await CreateAndSaveVehicleAsync(group.Id);
        
        var vehicles = await DbContext.Vehicles
            .Where(v => v.GroupId == group.Id)
            .ToListAsync();
        vehicles.Should().HaveCount(2);

        // 3. Schedule Maintenance
        var maintenance = new MaintenanceRecord
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle1.Id,
            ServiceType = MaintenanceServiceType.GeneralService,
            ScheduledDate = DateTime.UtcNow.AddDays(7),
            Status = MaintenanceStatus.Scheduled,
            Notes = "Regular service",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        DbContext.MaintenanceRecords.Add(maintenance);
        await DbContext.SaveChangesAsync();

        maintenance.Status.Should().Be(MaintenanceStatus.Scheduled);
        
        var scheduledMaintenance = await DbContext.MaintenanceRecords
            .Where(m => m.VehicleId == vehicle1.Id && m.Status == MaintenanceStatus.Scheduled)
            .ToListAsync();
        scheduledMaintenance.Should().HaveCount(1);
    }

    [Fact]
    [Trait("Category", "UserJourney")]
    public async Task ExpenseAndProposalWorkflow_CreateExpenseSplitCostsPayInvoices_ShouldSucceed()
    {
        // Setup
        var creator = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member1 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member2 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var group = await CreateAndSaveGroupAsync(
            creator.Id,
            new List<Guid> { creator.Id, member1.Id, member2.Id }
        );
        var vehicle = await CreateAndSaveVehicleAsync(group.Id);

        // 1. Create Expense
        var expense = TestDataBuilder.CreateTestExpense(group.Id, vehicle.Id, 300.00m, ExpenseType.Fuel);
        DbContext.Expenses.Add(expense);
        await DbContext.SaveChangesAsync();

        // 2. Split Costs (create invoice per member)
        var groupMembers = await DbContext.GroupMembers
            .Where(m => m.GroupId == group.Id)
            .ToListAsync();

        var invoices = groupMembers.Select(m => new Invoice
        {
            Id = Guid.NewGuid(),
            ExpenseId = expense.Id,
            PayerId = m.UserId,
            Amount = expense.Amount * m.SharePercentage,
            InvoiceNumber = $"INV-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
            Status = InvoiceStatus.Pending,
            DueDate = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        DbContext.Invoices.AddRange(invoices);
        await DbContext.SaveChangesAsync();

        // Verify split
        invoices.Count.Should().Be(3);
        invoices.Sum(i => i.Amount).Should().Be(300.00m);
        invoices.All(i => Math.Abs(i.Amount - 100.00m) < 0.01m).Should().BeTrue();

        // 3. Pay Invoices
        foreach (var invoice in invoices)
        {
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                PayerId = invoice.PayerId,
                Amount = invoice.Amount,
                Method = PaymentMethod.BankTransfer,
                Status = PaymentStatus.Completed,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            DbContext.Payments.Add(payment);
            invoice.Status = InvoiceStatus.Paid;
        }

        await DbContext.SaveChangesAsync();

        // Verify all paid
        var updatedInvoices = await DbContext.Invoices
            .Where(i => invoices.Select(inv => inv.Id).Contains(i.Id))
            .ToListAsync();
        updatedInvoices.All(i => i.Status == InvoiceStatus.Paid).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "UserJourney")]
    public async Task ProposalWorkflow_CreateProposalVoteClose_ShouldSucceed()
    {
        // Setup
        var creator = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member1 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member2 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member3 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var group = await CreateAndSaveGroupAsync(
            creator.Id,
            new List<Guid> { creator.Id, member1.Id, member2.Id, member3.Id }
        );

        var groupMembers = await DbContext.GroupMembers
            .Where(m => m.GroupId == group.Id)
            .ToListAsync();

        // 1. Create Proposal
        var proposal = TestDataBuilder.CreateTestProposal(
            group.Id, 
            creator.Id, 
            ProposalType.MaintenanceBudget
        );
        DbContext.Proposals.Add(proposal);
        await DbContext.SaveChangesAsync();

        proposal.Status.Should().Be(ProposalStatus.Active);

        // 2. Vote
        var votes = new List<Vote>();
        foreach (var member in groupMembers)
        {
            var voteChoice = member.UserId == creator.Id || member.UserId == member1.Id 
                ? VoteChoice.Yes 
                : VoteChoice.No;
            
            var vote = TestDataBuilder.CreateVote(
                proposal.Id, 
                member.UserId, 
                voteChoice, 
                member.SharePercentage
            );
            votes.Add(vote);
        }

        DbContext.Votes.AddRange(votes);
        await DbContext.SaveChangesAsync();

        // Calculate voting results
        var yesVotes = votes.Where(v => v.Choice == VoteChoice.Yes).Sum(v => v.Weight);
        var totalWeight = votes.Sum(v => v.Weight);
        var approvalPercentage = yesVotes / totalWeight;

        approvalPercentage.Should().BeGreaterThan(0.5m); // At least 2 out of 4 voted yes

        // 3. Close Proposal
        proposal.Status = approvalPercentage >= proposal.RequiredMajority 
            ? ProposalStatus.Passed 
            : ProposalStatus.Rejected;
        proposal.UpdatedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();

        proposal.Status.Should().Be(ProposalStatus.Passed);
    }
}


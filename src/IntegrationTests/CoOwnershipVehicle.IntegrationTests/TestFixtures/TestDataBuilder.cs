using CoOwnershipVehicle.Domain.Entities;
using Bogus;

namespace CoOwnershipVehicle.IntegrationTests.TestFixtures;

public class TestDataBuilder
{
    private static readonly Faker _faker = new();

    public static User CreateTestUser(UserRole role = UserRole.CoOwner, KycStatus kycStatus = KycStatus.Pending)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = _faker.Internet.Email(),
            UserName = _faker.Internet.UserName(),
            FirstName = _faker.Name.FirstName(),
            LastName = _faker.Name.LastName(),
            PhoneNumber = _faker.Phone.PhoneNumber(),
            Role = role,
            KycStatus = kycStatus,
            AccountStatus = UserAccountStatus.Active,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow.AddDays(-_faker.Random.Int(1, 90)),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static OwnershipGroup CreateTestGroup(Guid createdBy, string? name = null)
    {
        return new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"Test Group {_faker.Random.AlphaNumeric(8)}",
            Description = _faker.Lorem.Sentence(),
            Status = GroupStatus.Active,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow.AddDays(-_faker.Random.Int(1, 180)),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static GroupMember CreateGroupMember(Guid groupId, Guid userId, decimal sharePercentage, GroupRole role = GroupRole.Member)
    {
        return new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            SharePercentage = sharePercentage,
            RoleInGroup = role,
            JoinedAt = DateTime.UtcNow.AddDays(-_faker.Random.Int(1, 90)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Vehicle CreateTestVehicle(Guid? groupId = null, VehicleStatus status = VehicleStatus.Available)
    {
        return new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = _faker.Vehicle.Vin(),
            PlateNumber = _faker.Random.AlphaNumeric(8).ToUpper(),
            Model = _faker.Vehicle.Model(),
            Year = _faker.Random.Int(2020, 2024),
            Color = _faker.Commerce.Color(),
            Status = status,
            Odometer = _faker.Random.Int(0, 50000),
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow.AddDays(-_faker.Random.Int(1, 365)),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Booking CreateTestBooking(Guid vehicleId, Guid groupId, Guid userId, BookingStatus status = BookingStatus.Confirmed)
    {
        var startDate = DateTime.UtcNow.AddHours(_faker.Random.Int(1, 48));
        var endDate = startDate.AddHours(_faker.Random.Int(2, 24));
        
        return new Booking
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            GroupId = groupId,
            UserId = userId,
            StartAt = startDate,
            EndAt = endDate,
            Status = status,
            PriorityScore = (decimal)_faker.Random.Double(0, 100),
            Notes = _faker.Lorem.Sentence(),
            Purpose = _faker.Lorem.Word(),
            IsEmergency = false,
            Priority = BookingPriority.Normal,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static CheckIn CreateCheckIn(Guid bookingId, Guid userId, CheckInType type, int odometer)
    {
        return new CheckIn
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            UserId = userId,
            Type = type,
            Odometer = odometer,
            Notes = type == CheckInType.CheckOut ? "Vehicle checked out" : "Vehicle checked in",
            CheckInTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Expense CreateTestExpense(Guid groupId, Guid? vehicleId, decimal amount, ExpenseType type = ExpenseType.Maintenance)
    {
        return new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            VehicleId = vehicleId,
            Amount = amount,
            Description = _faker.Lorem.Sentence(),
            ExpenseType = type,
            DateIncurred = DateTime.UtcNow.AddDays(-_faker.Random.Int(1, 30)),
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Proposal CreateTestProposal(Guid groupId, Guid createdBy, ProposalType type = ProposalType.MaintenanceBudget)
    {
        return new Proposal
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            CreatedBy = createdBy,
            Title = _faker.Lorem.Sentence(5),
            Description = _faker.Lorem.Paragraph(),
            Type = type,
            Amount = _faker.Random.Decimal(100, 5000),
            Status = ProposalStatus.Active,
            VotingStartDate = DateTime.UtcNow,
            VotingEndDate = DateTime.UtcNow.AddDays(7),
            RequiredMajority = 0.5m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Vote CreateVote(Guid proposalId, Guid voterId, VoteChoice choice, decimal weight = 0.25m)
    {
        return new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = proposalId,
            VoterId = voterId,
            Weight = weight,
            Choice = choice,
            Comment = _faker.Lorem.Sentence(),
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static KycDocument CreateKycDocument(Guid userId, KycDocumentType type = KycDocumentType.GovernmentId)
    {
        return new KycDocument
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentType = type,
            DocumentNumber = _faker.Random.AlphaNumeric(10),
            FileUrl = $"https://storage.example.com/documents/{Guid.NewGuid()}.pdf",
            Status = KycDocumentStatus.Pending,
            UploadedAt = DateTime.UtcNow.AddDays(-_faker.Random.Int(1, 7)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Dispute CreateDispute(Guid groupId, Guid reportedBy, DisputeCategory category = DisputeCategory.VehicleDamage)
    {
        return new Dispute
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ReportedBy = reportedBy,
            Subject = _faker.Lorem.Sentence(4),
            Description = _faker.Lorem.Paragraph(),
            Category = category,
            Priority = DisputePriority.Medium,
            Status = DisputeStatus.Open,
            CreatedAt = DateTime.UtcNow.AddDays(-_faker.Random.Int(1, 14)),
            UpdatedAt = DateTime.UtcNow
        };
    }
}


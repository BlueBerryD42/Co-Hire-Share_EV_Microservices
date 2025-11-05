using Bogus;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.IntegrationTests.TestFixtures;

public static class TestDataBuilder
{
    private static readonly Faker _faker = new();

    public static User CreateTestUser(UserRole role = UserRole.CoOwner, KycStatus kycStatus = KycStatus.Pending)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            UserName = _faker.Internet.Email(),
            Email = _faker.Internet.Email(),
            FirstName = _faker.Name.FirstName(),
            LastName = _faker.Name.LastName(),
            PhoneNumber = _faker.Phone.PhoneNumber(),
            Phone = _faker.Phone.PhoneNumber(),
            Address = _faker.Address.StreetAddress(),
            City = _faker.Address.City(),
            Country = _faker.Address.Country(),
            PostalCode = _faker.Address.ZipCode(),
            DateOfBirth = _faker.Date.Past(30, DateTime.UtcNow.AddYears(-18)),
            Role = role,
            KycStatus = kycStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static OwnershipGroup CreateTestGroup(Guid createdBy)
    {
        return new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = _faker.Company.CompanyName() + " Group",
            Description = _faker.Lorem.Sentence(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static GroupMember CreateGroupMember(Guid groupId, Guid userId, decimal sharePercentage, GroupRole role)
    {
        return new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            UserId = userId,
            SharePercentage = sharePercentage,
            RoleInGroup = role,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Vehicle CreateTestVehicle(Guid? groupId = null)
    {
        return new Vehicle
        {
            Id = Guid.NewGuid(),
            GroupId = groupId ?? Guid.NewGuid(),
            Model = _faker.Vehicle.Manufacturer() + " " + _faker.Vehicle.Model(),
            Year = _faker.Date.Past(10).Year,
            PlateNumber = _faker.Random.AlphaNumeric(7).ToUpper(),
            Vin = _faker.Vehicle.Vin(),
            Color = _faker.Commerce.Color(),
            Status = VehicleStatus.Available,
            Odometer = _faker.Random.Int(0, 100000),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Booking CreateTestBooking(
        Guid vehicleId, 
        Guid groupId, 
        Guid userId, 
        BookingStatus status = BookingStatus.Pending)
    {
        var startAt = DateTime.UtcNow.AddDays(1);
        return new Booking
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            GroupId = groupId,
            UserId = userId,
            StartAt = startAt,
            EndAt = startAt.AddHours(24),
            Status = status,
            PriorityScore = 0,
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
            Notes = _faker.Lorem.Sentence(),
            CheckInTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Expense CreateTestExpense(Guid groupId, Guid vehicleId, decimal amount, ExpenseType expenseType)
    {
        return new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            VehicleId = vehicleId,
            ExpenseType = expenseType,
            Amount = amount,
            Description = _faker.Lorem.Sentence(),
            DateIncurred = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            Notes = _faker.Lorem.Sentence(),
            IsRecurring = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static KycDocument CreateKycDocument(Guid userId)
    {
        return new KycDocument
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentType = KycDocumentType.DriverLicense,
            FileName = _faker.System.FileName("pdf"),
            StorageUrl = _faker.Internet.Url(),
            Status = KycDocumentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Proposal CreateTestProposal(Guid groupId, Guid createdBy, ProposalType type)
    {
        var startDate = DateTime.UtcNow;
        return new Proposal
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            CreatedBy = createdBy,
            Title = _faker.Lorem.Sentence(3),
            Description = _faker.Lorem.Paragraph(),
            Type = type,
            Amount = _faker.Finance.Amount(100, 10000),
            Status = ProposalStatus.Active,
            VotingStartDate = startDate,
            VotingEndDate = startDate.AddDays(7),
            RequiredMajority = 0.5m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static Vote CreateVote(Guid proposalId, Guid voterId, VoteChoice choice, decimal weight)
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

    public static Dispute CreateDispute(Guid groupId, Guid reportedBy, DisputeCategory category)
    {
        return new Dispute
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ReportedBy = reportedBy,
            Subject = _faker.Lorem.Sentence(3),
            Description = _faker.Lorem.Paragraph(),
            Category = category,
            Priority = DisputePriority.Medium,
            Status = DisputeStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}

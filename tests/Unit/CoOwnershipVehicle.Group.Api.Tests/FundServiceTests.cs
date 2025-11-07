using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CoOwnershipVehicle.Group.Api.Tests;

public class FundServiceTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Mock<ILogger<FundService>> _loggerMock;
    private readonly FundService _fundService;
    private readonly Guid _testUserId;
    private readonly Guid _testAdminId;
    private readonly Guid _testGroupId;

    public FundServiceTests()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new GroupDbContext(options);
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<FundService>>();
        _fundService = new FundService(_context, _publishEndpointMock.Object, _loggerMock.Object);
        _testUserId = Guid.NewGuid();
        _testAdminId = Guid.NewGuid();
        _testGroupId = Guid.NewGuid();

        SeedTestData();
    }

    private void SeedTestData()
    {
        var user = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var admin = new User
        {
            Id = _testAdminId,
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(admin);

        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Test Group",
            Status = GroupStatus.Active,
            CreatedBy = _testAdminId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);

        var member = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _testUserId,
            SharePercentage = 0.5m,
            RoleInGroup = GroupRole.Member,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(member);

        var adminMember = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _testAdminId,
            SharePercentage = 0.5m,
            RoleInGroup = GroupRole.Admin,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(adminMember);

        _context.SaveChanges();
    }

    [Fact]
    public async Task GetFundBalance_ShouldAutoCreateFund_WhenNotExists()
    {
        // Act
        var result = await _fundService.GetFundBalanceAsync(_testGroupId, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.TotalBalance.Should().Be(0m);
        result.ReserveBalance.Should().Be(0m);
        result.AvailableBalance.Should().Be(0m);

        var fund = await _context.GroupFunds.FirstOrDefaultAsync(f => f.GroupId == _testGroupId);
        fund.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFundBalance_ShouldReturnForbidden_WhenNotMember()
    {
        // Act
        var act = async () => await _fundService.GetFundBalanceAsync(_testGroupId, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DepositFund_ShouldUpdateBalance_WhenValid()
    {
        // Arrange
        var depositDto = new DepositFundDto
        {
            Amount = 500m,
            Description = "Test deposit"
        };

        // Act
        var result = await _fundService.DepositFundAsync(_testGroupId, depositDto, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(500m);
        result.Type.Should().Be(FundTransactionType.Deposit);
        result.Status.Should().Be(FundTransactionStatus.Completed);

        var fund = await _context.GroupFunds.FirstOrDefaultAsync(f => f.GroupId == _testGroupId);
        fund.Should().NotBeNull();
        fund!.TotalBalance.Should().Be(500m);
        fund.AvailableBalance.Should().Be(500m);
    }

    [Fact]
    public async Task DepositFund_ShouldReturnError_WhenAmountInvalid()
    {
        // Arrange
        var depositDto = new DepositFundDto
        {
            Amount = -100m,
            Description = "Invalid deposit"
        };

        // Act
        var act = async () => await _fundService.DepositFundAsync(_testGroupId, depositDto, _testUserId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WithdrawFund_ShouldCreatePendingTransaction_WhenLargeAmount()
    {
        // Arrange
        // First deposit to have funds
        var fund = new GroupFund
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            TotalBalance = 2000m,
            ReserveBalance = 0m,
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupFunds.Add(fund);
        await _context.SaveChangesAsync();

        var withdrawDto = new WithdrawFundDto
        {
            Amount = 1500m, // Large amount requiring approval
            Reason = "Large withdrawal"
        };

        // Act
        var result = await _fundService.WithdrawFundAsync(_testGroupId, withdrawDto, _testAdminId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(FundTransactionStatus.Pending);
        result.Type.Should().Be(FundTransactionType.Withdrawal);
    }

    [Fact]
    public async Task WithdrawFund_ShouldReturnError_WhenNotAdmin()
    {
        // Arrange
        var withdrawDto = new WithdrawFundDto
        {
            Amount = 100m,
            Reason = "Test withdrawal"
        };

        // Act
        var act = async () => await _fundService.WithdrawFundAsync(_testGroupId, withdrawDto, _testUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task WithdrawFund_ShouldReturnError_WhenInsufficientBalance()
    {
        // Arrange
        var fund = new GroupFund
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            TotalBalance = 100m,
            ReserveBalance = 0m,
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupFunds.Add(fund);
        await _context.SaveChangesAsync();

        var withdrawDto = new WithdrawFundDto
        {
            Amount = 200m, // More than available
            Reason = "Test withdrawal"
        };

        // Act
        var act = async () => await _fundService.WithdrawFundAsync(_testGroupId, withdrawDto, _testAdminId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AllocateReserve_ShouldMoveFundsToReserve_WhenValid()
    {
        // Arrange
        var fund = new GroupFund
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            TotalBalance = 1000m,
            ReserveBalance = 0m,
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupFunds.Add(fund);
        await _context.SaveChangesAsync();

        var allocateDto = new AllocateReserveDto
        {
            Amount = 200m,
            Reason = "Emergency fund"
        };

        // Act
        var result = await _fundService.AllocateReserveAsync(_testGroupId, allocateDto, _testAdminId);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FundTransactionType.Allocation);

        var updatedFund = await _context.GroupFunds.FindAsync(fund.Id);
        updatedFund!.TotalBalance.Should().Be(1000m); // Unchanged
        updatedFund.ReserveBalance.Should().Be(200m);
        updatedFund.AvailableBalance.Should().Be(800m);
    }

    [Fact]
    public async Task ReleaseReserve_ShouldMoveFundsFromReserve_WhenValid()
    {
        // Arrange
        var fund = new GroupFund
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            TotalBalance = 1000m,
            ReserveBalance = 300m,
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupFunds.Add(fund);
        await _context.SaveChangesAsync();

        var releaseDto = new ReleaseReserveDto
        {
            Amount = 100m,
            Reason = "Need funds"
        };

        // Act
        var result = await _fundService.ReleaseReserveAsync(_testGroupId, releaseDto, _testAdminId);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(FundTransactionType.Release);

        var updatedFund = await _context.GroupFunds.FindAsync(fund.Id);
        updatedFund!.TotalBalance.Should().Be(1000m); // Unchanged
        updatedFund.ReserveBalance.Should().Be(200m);
        updatedFund.AvailableBalance.Should().Be(800m);
    }

    [Fact]
    public async Task GetTransactionHistory_ShouldReturnPaginatedResults()
    {
        // Arrange
        var fund = new GroupFund
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            TotalBalance = 0m,
            ReserveBalance = 0m,
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupFunds.Add(fund);

        // Create some transactions
        for (int i = 0; i < 5; i++)
        {
            var transaction = new FundTransaction
            {
                Id = Guid.NewGuid(),
                GroupId = _testGroupId,
                InitiatedBy = _testUserId,
                Type = FundTransactionType.Deposit,
                Amount = 100m,
                BalanceBefore = i * 100m,
                BalanceAfter = (i + 1) * 100m,
                Description = $"Deposit {i}",
                Status = FundTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow.AddDays(-i),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.FundTransactions.Add(transaction);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _fundService.GetTransactionHistoryAsync(_testGroupId, _testUserId, page: 1, pageSize: 3);

        // Assert
        result.Should().NotBeNull();
        result.Transactions.Should().HaveCount(3);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}




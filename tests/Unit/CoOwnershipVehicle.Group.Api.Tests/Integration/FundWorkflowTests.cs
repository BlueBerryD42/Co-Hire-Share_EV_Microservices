using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CoOwnershipVehicle.Group.Api.Tests.Integration;

public class FundWorkflowTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Mock<ILogger<FundService>> _loggerMock;
    private readonly FundService _fundService;
    private readonly Guid _testUserId;
    private readonly Guid _testAdminId;
    private readonly Guid _testGroupId;

    public FundWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new GroupDbContext(options);
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<FundService>>();
        _testUserId = Guid.NewGuid();
        _testAdminId = Guid.NewGuid();
        _testGroupId = Guid.NewGuid();

        // Mock IUserServiceClient
        var userServiceClientMock = new Mock<IUserServiceClient>();
        userServiceClientMock.Setup(x => x.GetUserAsync(_testUserId, It.IsAny<string>()))
            .ReturnsAsync(new UserInfoDto { Id = _testUserId, Email = "test@example.com", FirstName = "Test", LastName = "User" });
        userServiceClientMock.Setup(x => x.GetUserAsync(_testAdminId, It.IsAny<string>()))
            .ReturnsAsync(new UserInfoDto { Id = _testAdminId, Email = "admin@example.com", FirstName = "Admin", LastName = "User" });
        userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<List<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync((List<Guid> userIds, string token) => userIds.ToDictionary(
                id => id,
                id => id == _testUserId 
                    ? new UserInfoDto { Id = _testUserId, Email = "test@example.com", FirstName = "Test", LastName = "User" }
                    : new UserInfoDto { Id = _testAdminId, Email = "admin@example.com", FirstName = "Admin", LastName = "User" }));

        // Mock IHttpContextAccessor
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer test-token";
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        _fundService = new FundService(
            _context, 
            _publishEndpointMock.Object, 
            _loggerMock.Object,
            userServiceClientMock.Object,
            httpContextAccessorMock.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        // Note: Users are no longer stored in GroupDbContext - they're fetched via HTTP

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
    public async Task DepositToBalanceUpdate_ShouldMaintainIntegrity()
    {
        // Arrange
        var depositDto = new DepositFundDto
        {
            Amount = 1000m,
            Description = "Initial deposit"
        };

        // Act
        var transaction = await _fundService.DepositFundAsync(_testGroupId, depositDto, _testUserId);

        // Assert
        transaction.Should().NotBeNull();
        transaction.Status.Should().Be(FundTransactionStatus.Completed);

        var fund = await _context.GroupFunds.FirstOrDefaultAsync(f => f.GroupId == _testGroupId);
        fund.Should().NotBeNull();
        fund!.TotalBalance.Should().Be(1000m);
        fund.AvailableBalance.Should().Be(1000m);
        fund.ReserveBalance.Should().Be(0m);

        // Verify transaction record
        var savedTransaction = await _context.FundTransactions.FindAsync(transaction.Id);
        savedTransaction.Should().NotBeNull();
        savedTransaction!.BalanceAfter.Should().Be(1000m);
        savedTransaction.BalanceBefore.Should().Be(0m);
    }

    [Fact]
    public async Task WithdrawalApprovalWorkflow_ShouldUpdateBalance()
    {
        // Arrange - First deposit
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
            Amount = 500m,
            Reason = "Test withdrawal"
        };

        // Act - Create withdrawal (small amount, auto-approved)
        var transaction = await _fundService.WithdrawFundAsync(_testGroupId, withdrawDto, _testAdminId);

        // Assert
        transaction.Should().NotBeNull();
        
        var updatedFund = await _context.GroupFunds.FindAsync(fund.Id);
        updatedFund!.TotalBalance.Should().Be(1500m);
        updatedFund.AvailableBalance.Should().Be(1500m);
    }

    [Fact]
    public async Task ConcurrentDeposits_ShouldHandleRaceConditions()
    {
        // Arrange
        var deposit1 = new DepositFundDto { Amount = 500m, Description = "Deposit 1" };
        var deposit2 = new DepositFundDto { Amount = 300m, Description = "Deposit 2" };

        // Act - Simulate concurrent deposits
        var task1 = _fundService.DepositFundAsync(_testGroupId, deposit1, _testUserId);
        var task2 = _fundService.DepositFundAsync(_testGroupId, deposit2, _testUserId);

        await Task.WhenAll(task1, task2);

        // Assert
        var fund = await _context.GroupFunds.FirstOrDefaultAsync(f => f.GroupId == _testGroupId);
        fund.Should().NotBeNull();
        fund!.TotalBalance.Should().Be(800m);

        var transactions = await _context.FundTransactions
            .Where(t => t.GroupId == _testGroupId && t.Type == FundTransactionType.Deposit)
            .ToListAsync();
        transactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task FundBalanceIntegrity_ShouldMaintainTotalEqualsReservePlusAvailable()
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

        // Act - Allocate to reserve
        var allocateDto = new AllocateReserveDto
        {
            Amount = 300m,
            Reason = "Emergency fund"
        };
        await _fundService.AllocateReserveAsync(_testGroupId, allocateDto, _testAdminId);

        // Assert
        var updatedFund = await _context.GroupFunds.FindAsync(fund.Id);
        updatedFund!.TotalBalance.Should().Be(1000m);
        updatedFund.ReserveBalance.Should().Be(300m);
        updatedFund.AvailableBalance.Should().Be(700m);
        (updatedFund.ReserveBalance + updatedFund.AvailableBalance).Should().Be(updatedFund.TotalBalance);
    }

    [Fact]
    public async Task TransactionHistory_ShouldSumToCurrentBalance()
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
        await _context.SaveChangesAsync();

        // Act - Create multiple transactions
        await _fundService.DepositFundAsync(_testGroupId, new DepositFundDto { Amount = 1000m, Description = "Deposit 1" }, _testUserId);
        await _fundService.DepositFundAsync(_testGroupId, new DepositFundDto { Amount = 500m, Description = "Deposit 2" }, _testUserId);
        await _fundService.AllocateReserveAsync(_testGroupId, new AllocateReserveDto { Amount = 300m, Reason = "Reserve" }, _testAdminId);

        // Assert
        var finalFund = await _context.GroupFunds.FindAsync(fund.Id);
        finalFund!.TotalBalance.Should().Be(1500m);
        finalFund.ReserveBalance.Should().Be(300m);
        finalFund.AvailableBalance.Should().Be(1200m);

        var transactions = await _context.FundTransactions
            .Where(t => t.GroupId == _testGroupId && t.Status == FundTransactionStatus.Completed)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();

        var lastTransaction = transactions.Last();
        lastTransaction.BalanceAfter.Should().Be(finalFund.TotalBalance);
    }

    [Fact]
    public async Task OverdraftPrevention_ShouldRejectInvalidWithdrawal()
    {
        // Arrange
        var fund = new GroupFund
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            TotalBalance = 500m,
            ReserveBalance = 200m, // Available = 300m
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupFunds.Add(fund);
        await _context.SaveChangesAsync();

        var withdrawDto = new WithdrawFundDto
        {
            Amount = 400m, // More than available (300m)
            Reason = "Invalid withdrawal"
        };

        // Act
        var act = async () => await _fundService.WithdrawFundAsync(_testGroupId, withdrawDto, _testAdminId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        
        var unchangedFund = await _context.GroupFunds.FindAsync(fund.Id);
        unchangedFund!.TotalBalance.Should().Be(500m); // Unchanged
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}




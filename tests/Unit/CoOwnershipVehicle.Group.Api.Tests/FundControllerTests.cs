using System.Security.Claims;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Controllers;
using CoOwnershipVehicle.Group.Api.Contracts;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CoOwnershipVehicle.Group.Api.Tests;

public class FundControllerTests : IDisposable
{
    private readonly Mock<IFundService> _fundServiceMock;
    private readonly Mock<ILogger<FundController>> _loggerMock;
    private readonly FundController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testGroupId;

    public FundControllerTests()
    {
        _fundServiceMock = new Mock<IFundService>();
        _loggerMock = new Mock<ILogger<FundController>>();
        _testUserId = Guid.NewGuid();
        _testGroupId = Guid.NewGuid();

        _controller = new FundController(_fundServiceMock.Object, _loggerMock.Object);
        
        // Setup user claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    [Fact]
    public async Task GetFundBalance_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var balanceDto = new FundBalanceDto
        {
            GroupId = _testGroupId,
            TotalBalance = 1000m,
            ReserveBalance = 200m,
            AvailableBalance = 800m,
            LastUpdated = DateTime.UtcNow
        };

        _fundServiceMock
            .Setup(x => x.GetFundBalanceAsync(_testGroupId, _testUserId))
            .ReturnsAsync(balanceDto);

        // Act
        var result = await _controller.GetFundBalance(_testGroupId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedBalance = okResult!.Value as FundBalanceDto;
        returnedBalance.Should().NotBeNull();
        returnedBalance!.TotalBalance.Should().Be(1000m);
        returnedBalance.AvailableBalance.Should().Be(800m);
    }

    [Fact]
    public async Task GetFundBalance_ShouldReturnForbidden_WhenUnauthorized()
    {
        // Arrange
        _fundServiceMock
            .Setup(x => x.GetFundBalanceAsync(_testGroupId, _testUserId))
            .ThrowsAsync(new UnauthorizedAccessException("User is not a member"));

        // Act
        var result = await _controller.GetFundBalance(_testGroupId);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task DepositFund_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var depositDto = new DepositFundDto
        {
            Amount = 500m,
            Description = "Test deposit"
        };

        var transactionDto = new FundTransactionDto
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            Type = FundTransactionType.Deposit,
            Amount = 500m,
            Status = FundTransactionStatus.Completed,
            TransactionDate = DateTime.UtcNow
        };

        _fundServiceMock
            .Setup(x => x.DepositFundAsync(_testGroupId, depositDto, _testUserId))
            .ReturnsAsync(transactionDto);

        // Act
        var result = await _controller.DepositFund(_testGroupId, depositDto);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedTransaction = okResult!.Value as FundTransactionDto;
        returnedTransaction.Should().NotBeNull();
        returnedTransaction!.Amount.Should().Be(500m);
    }

    [Fact]
    public async Task DepositFund_ShouldReturnBadRequest_WhenAmountInvalid()
    {
        // Arrange
        var depositDto = new DepositFundDto
        {
            Amount = -100m, // Invalid
            Description = "Test deposit"
        };

        _controller.ModelState.AddModelError("Amount", "Amount must be greater than 0");

        // Act
        var result = await _controller.DepositFund(_testGroupId, depositDto);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task WithdrawFund_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var withdrawDto = new WithdrawFundDto
        {
            Amount = 200m,
            Reason = "Test withdrawal"
        };

        var transactionDto = new FundTransactionDto
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            Type = FundTransactionType.Withdrawal,
            Amount = 200m,
            Status = FundTransactionStatus.Pending,
            TransactionDate = DateTime.UtcNow
        };

        _fundServiceMock
            .Setup(x => x.WithdrawFundAsync(_testGroupId, withdrawDto, _testUserId))
            .ReturnsAsync(transactionDto);

        // Act
        var result = await _controller.WithdrawFund(_testGroupId, withdrawDto);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedTransaction = okResult!.Value as FundTransactionDto;
        returnedTransaction.Should().NotBeNull();
        returnedTransaction!.Type.Should().Be(FundTransactionType.Withdrawal);
    }

    [Fact]
    public async Task WithdrawFund_ShouldReturnForbidden_WhenNotAdmin()
    {
        // Arrange
        var withdrawDto = new WithdrawFundDto
        {
            Amount = 200m,
            Reason = "Test withdrawal"
        };

        _fundServiceMock
            .Setup(x => x.WithdrawFundAsync(_testGroupId, withdrawDto, _testUserId))
            .ThrowsAsync(new UnauthorizedAccessException("Only admins can withdraw"));

        // Act
        var result = await _controller.WithdrawFund(_testGroupId, withdrawDto);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AllocateReserve_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var allocateDto = new AllocateReserveDto
        {
            Amount = 100m,
            Reason = "Emergency fund"
        };

        var transactionDto = new FundTransactionDto
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            Type = FundTransactionType.Allocation,
            Amount = 100m,
            Status = FundTransactionStatus.Completed,
            TransactionDate = DateTime.UtcNow
        };

        _fundServiceMock
            .Setup(x => x.AllocateReserveAsync(_testGroupId, allocateDto, _testUserId))
            .ReturnsAsync(transactionDto);

        // Act
        var result = await _controller.AllocateReserve(_testGroupId, allocateDto);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedTransaction = okResult!.Value as FundTransactionDto;
        returnedTransaction.Should().NotBeNull();
        returnedTransaction!.Type.Should().Be(FundTransactionType.Allocation);
    }

    [Fact]
    public async Task ReleaseReserve_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var releaseDto = new ReleaseReserveDto
        {
            Amount = 50m,
            Reason = "Need funds"
        };

        var transactionDto = new FundTransactionDto
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            Type = FundTransactionType.Release,
            Amount = 50m,
            Status = FundTransactionStatus.Completed,
            TransactionDate = DateTime.UtcNow
        };

        _fundServiceMock
            .Setup(x => x.ReleaseReserveAsync(_testGroupId, releaseDto, _testUserId))
            .ReturnsAsync(transactionDto);

        // Act
        var result = await _controller.ReleaseReserve(_testGroupId, releaseDto);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedTransaction = okResult!.Value as FundTransactionDto;
        returnedTransaction.Should().NotBeNull();
        returnedTransaction!.Type.Should().Be(FundTransactionType.Release);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}




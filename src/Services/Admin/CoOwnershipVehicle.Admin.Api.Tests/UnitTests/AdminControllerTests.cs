using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Admin.Api.Controllers;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Admin.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System.Security.Claims;

namespace CoOwnershipVehicle.Admin.Api.Tests.UnitTests;

public class AdminControllerTests : IDisposable
{
    private readonly AdminDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<AdminController>> _loggerMock;
    private readonly Mock<IAdminService> _adminServiceMock;
    private readonly AdminController _controller;
    private readonly DbContextOptions<AdminDbContext> _options;

    public AdminControllerTests()
    {
        _options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AdminDbContext(_options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<AdminController>>();
        _adminServiceMock = new Mock<IAdminService>();

        _controller = new AdminController(_adminServiceMock.Object, _loggerMock.Object);
        
        // Setup default user context
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "SystemAdmin")
        }, "Test"));
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }

    #region Dashboard Tests

    [Fact]
    public async Task GetDashboard_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };
        var expectedMetrics = new DashboardMetricsDto
        {
            Users = new UserMetricsDto { TotalUsers = 100 },
            Groups = new GroupMetricsDto { TotalGroups = 50 }
        };

        _adminServiceMock.Setup(s => s.GetDashboardMetricsAsync(request))
            .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _controller.GetDashboard(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedMetrics);
    }

    [Fact]
    public async Task GetDashboard_WhenServiceThrows_ReturnsInternalServerError()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };
        _adminServiceMock.Setup(s => s.GetDashboardMetricsAsync(request))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetDashboard(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    #endregion

    #region User Management Tests

    [Fact]
    public async Task GetUsers_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new UserListRequestDto { Page = 1, PageSize = 20 };
        var expectedResponse = new UserListResponseDto
        {
            Users = new List<UserSummaryDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 20,
            TotalPages = 0
        };

        _adminServiceMock.Setup(s => s.GetUsersAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetUsers(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserDetails_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedUser = new UserDetailsDto
        {
            Id = userId,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User"
        };

        _adminServiceMock.Setup(s => s.GetUserDetailsAsync(userId))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _controller.GetUserDetails(userId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserDetails_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _adminServiceMock.Setup(s => s.GetUserDetailsAsync(userId))
            .ThrowsAsync(new ArgumentException("User not found"));

        // Act
        var result = await _controller.GetUserDetails(userId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateUserStatus_WithValidData_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var request = new UpdateUserStatusDto
        {
            Status = UserAccountStatus.Suspended,
            Reason = "Policy violation"
        };

        SetupCurrentUser(adminUserId);
        _adminServiceMock.Setup(s => s.UpdateUserStatusAsync(userId, request, adminUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdateUserStatus(userId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateUserStatus_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var request = new UpdateUserStatusDto
        {
            Status = UserAccountStatus.Suspended,
            Reason = "Policy violation"
        };

        SetupCurrentUser(adminUserId);
        _adminServiceMock.Setup(s => s.UpdateUserStatusAsync(userId, request, adminUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.UpdateUserStatus(userId, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateUserRole_WithValidData_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var request = new UpdateUserRoleDto
        {
            Role = UserRole.GroupAdmin,
            Reason = "Promotion"
        };

        SetupCurrentUser(adminUserId);
        _adminServiceMock.Setup(s => s.UpdateUserRoleAsync(userId, request, adminUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdateUserRole(userId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPendingKycUsers_ReturnsOkResult()
    {
        // Arrange
        var expectedUsers = new List<PendingKycUserDto>();
        _adminServiceMock.Setup(s => s.GetPendingKycUsersAsync())
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _controller.GetPendingKycUsers();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Group Management Tests

    [Fact]
    public async Task GetGroups_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new GroupListRequestDto { Page = 1, PageSize = 20 };
        var expectedResponse = new GroupListResponseDto
        {
            Groups = new List<GroupSummaryDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 20,
            TotalPages = 0
        };

        _adminServiceMock.Setup(s => s.GetGroupsAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetGroups(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetGroupDetails_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var expectedGroup = new GroupDetailsDto
        {
            Id = groupId,
            Name = "Test Group"
        };

        _adminServiceMock.Setup(s => s.GetGroupDetailsAsync(groupId))
            .ReturnsAsync(expectedGroup);

        // Act
        var result = await _controller.GetGroupDetails(groupId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateGroupStatus_WithValidData_ReturnsOk()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var request = new UpdateGroupStatusDto
        {
            Status = GroupStatus.Inactive,
            Reason = "Maintenance"
        };

        SetupCurrentUser(adminUserId);
        _adminServiceMock.Setup(s => s.UpdateGroupStatusAsync(groupId, request, adminUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.UpdateGroupStatus(groupId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetGroupHealth_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var expectedHealth = new GroupHealthDto
        {
            Status = GroupHealthStatus.Healthy,
            Score = 95,
            Recommendation = "Group is operating well"
        };

        _adminServiceMock.Setup(s => s.GetGroupHealthAsync(groupId))
            .ReturnsAsync(expectedHealth);

        // Act
        var result = await _controller.GetGroupHealth(groupId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Dispute Management Tests

    [Fact]
    public async Task CreateDispute_WithValidData_ReturnsCreatedResult()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var request = new CreateDisputeDto
        {
            GroupId = Guid.NewGuid(),
            Subject = "Test Dispute",
            Description = "Test Description",
            Category = DisputeCategory.Financial,
            Priority = DisputePriority.High
        };

        SetupCurrentUser(adminUserId);
        _adminServiceMock.Setup(s => s.CreateDisputeAsync(request, adminUserId))
            .ReturnsAsync(disputeId);

        // Act
        var result = await _controller.CreateDispute(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDisputes_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new DisputeListRequestDto { Page = 1, PageSize = 20 };
        var expectedResponse = new DisputeListResponseDto
        {
            Disputes = new List<DisputeSummaryDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 20,
            TotalPages = 0
        };

        _adminServiceMock.Setup(s => s.GetDisputesAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetDisputes(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDisputeDetails_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var disputeId = Guid.NewGuid();
        var expectedDispute = new DisputeDetailsDto
        {
            Id = disputeId,
            Subject = "Test Dispute",
            Status = DisputeStatus.Open
        };

        _adminServiceMock.Setup(s => s.GetDisputeDetailsAsync(disputeId))
            .ReturnsAsync(expectedDispute);

        // Act
        var result = await _controller.GetDisputeDetails(disputeId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AssignDispute_WithValidData_ReturnsOk()
    {
        // Arrange
        var disputeId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var request = new AssignDisputeDto
        {
            AssignedTo = Guid.NewGuid(),
            Note = "Assigning to staff member"
        };

        SetupCurrentUser(adminUserId);
        _adminServiceMock.Setup(s => s.AssignDisputeAsync(disputeId, request, adminUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.AssignDispute(disputeId, request);

        // Assert
        result.Result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddDisputeComment_WithValidData_ReturnsOk()
    {
        // Arrange
        var disputeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new AddDisputeCommentDto
        {
            Comment = "This is a test comment",
            IsInternal = false
        };

        SetupCurrentUser(userId);
        _adminServiceMock.Setup(s => s.AddDisputeCommentAsync(disputeId, request, userId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.AddDisputeComment(disputeId, request);

        // Assert
        result.Result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResolveDispute_WithValidData_ReturnsOk()
    {
        // Arrange
        var disputeId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var request = new ResolveDisputeDto
        {
            Resolution = "Dispute resolved",
            Note = "Issue was addressed"
        };

        SetupCurrentUser(adminUserId);
        _adminServiceMock.Setup(s => s.ResolveDisputeAsync(disputeId, request, adminUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ResolveDispute(disputeId, request);

        // Assert
        result.Result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDisputeStatistics_ReturnsOkResult()
    {
        // Arrange
        var expectedStats = new DisputeStatisticsDto
        {
            TotalDisputes = 100,
            OpenDisputes = 20,
            ResolvedDisputes = 75
        };

        _adminServiceMock.Setup(s => s.GetDisputeStatisticsAsync())
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetDisputeStatistics();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Financial Tests

    [Fact]
    public async Task GetFinancialOverview_ReturnsOkResult()
    {
        // Arrange
        var expectedOverview = new FinancialOverviewDto
        {
            TotalRevenueAllTime = 1000000,
            TotalRevenueMonth = 50000,
            FinancialHealthScore = 85
        };

        _adminServiceMock.Setup(s => s.GetFinancialOverviewAsync())
            .ReturnsAsync(expectedOverview);

        // Act
        var result = await _controller.GetFinancialOverview();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPaymentStatistics_ReturnsOkResult()
    {
        // Arrange
        var expectedStats = new PaymentStatisticsDto
        {
            SuccessRate = 95.5,
            FailureRate = 4.5,
            AverageAmount = 500
        };

        _adminServiceMock.Setup(s => s.GetPaymentStatisticsAsync())
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetPaymentStatistics();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetExpenseAnalysis_ReturnsOkResult()
    {
        // Arrange
        var expectedAnalysis = new ExpenseAnalysisDto
        {
            TotalByType = new Dictionary<ExpenseType, decimal>(),
            AverageCostPerVehicle = 2500
        };

        _adminServiceMock.Setup(s => s.GetExpenseAnalysisAsync())
            .ReturnsAsync(expectedAnalysis);

        // Act
        var result = await _controller.GetExpenseAnalysis();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFinancialAnomalies_ReturnsOkResult()
    {
        // Arrange
        var expectedAnomalies = new FinancialAnomaliesDto
        {
            UnusualTransactions = new List<PaymentAnomalyDto>(),
            SuspiciousPaymentPatterns = new List<SuspiciousPatternDto>()
        };

        _adminServiceMock.Setup(s => s.GetFinancialAnomaliesAsync())
            .ReturnsAsync(expectedAnomalies);

        // Act
        var result = await _controller.GetFinancialAnomalies();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GenerateFinancialPdf_WithValidRequest_ReturnsFileResult()
    {
        // Arrange
        var request = new FinancialReportRequestDto
        {
            Type = "Monthly",
            StartDate = DateTime.UtcNow.AddMonths(-1),
            EndDate = DateTime.UtcNow
        };

        var pdfBytes = new byte[] { 1, 2, 3, 4, 5 };
        _adminServiceMock.Setup(s => s.GenerateFinancialPdfAsync(request))
            .ReturnsAsync(pdfBytes);

        // Act
        var result = await _controller.GenerateFinancialPdf(request);

        // Assert
        result.Should().BeOfType<FileContentResult>();
    }

    [Fact]
    public async Task GenerateFinancialExcel_WithValidRequest_ReturnsFileResult()
    {
        // Arrange
        var request = new FinancialReportRequestDto
        {
            Type = "Monthly",
            StartDate = DateTime.UtcNow.AddMonths(-1),
            EndDate = DateTime.UtcNow
        };

        var excelBytes = new byte[] { 1, 2, 3, 4, 5 };
        _adminServiceMock.Setup(s => s.GenerateFinancialExcelAsync(request))
            .ReturnsAsync(excelBytes);

        // Act
        var result = await _controller.GenerateFinancialExcel(request);

        // Assert
        result.Should().BeOfType<FileContentResult>();
    }

    #endregion

    #region Comprehensive Financial Overview Tests

    [Fact]
    public async Task GetFinancialOverview_WithCompleteData_ReturnsAccurateTotals()
    {
        // Arrange
        var expectedOverview = new FinancialOverviewDto
        {
            TotalRevenueAllTime = 1000000,
            TotalRevenueMonth = 50000,
            TotalRevenueWeek = 10000,
            TotalRevenueDay = 1500,
            FinancialHealthScore = 85,
            PaymentSuccessRate = 95.5,
            FailedPaymentsCount = 5,
            FailedPaymentsAmount = 2500,
            PendingPaymentsCount = 10
        };

        _adminServiceMock.Setup(s => s.GetFinancialOverviewAsync())
            .ReturnsAsync(expectedOverview);

        // Act
        var result = await _controller.GetFinancialOverview();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var overview = okResult!.Value as FinancialOverviewDto;
        overview!.TotalRevenueAllTime.Should().Be(1000000);
        overview.PaymentSuccessRate.Should().Be(95.5);
    }

    [Fact]
    public async Task GetFinancialByGroups_ReturnsGroupBreakdown()
    {
        // Arrange
        var expectedBreakdown = new FinancialGroupBreakdownDto
        {
            Groups = new List<GroupFinancialItemDto>
            {
                new GroupFinancialItemDto
                {
                    GroupId = Guid.NewGuid(),
                    GroupName = "Test Group",
                    TotalExpenses = 5000,
                    FundBalance = 1000,
                    HasFinancialIssues = false,
                    PaymentComplianceRate = 95.0
                }
            }
        };

        _adminServiceMock.Setup(s => s.GetFinancialByGroupsAsync())
            .ReturnsAsync(expectedBreakdown);

        // Act
        var result = await _controller.GetFinancialByGroups();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetExpenseAnalysis_ReturnsAnalysis()
    {
        // Arrange
        var expectedAnalysis = new ExpenseAnalysisDto
        {
            TotalByType = new Dictionary<ExpenseType, decimal>
            {
                { ExpenseType.Fuel, 1000 },
                { ExpenseType.Maintenance, 2000 }
            },
            AverageCostPerVehicle = 2500,
            OptimizationOpportunities = new List<string> { "Reduce fuel costs" }
        };

        _adminServiceMock.Setup(s => s.GetExpenseAnalysisAsync())
            .ReturnsAsync(expectedAnalysis);

        // Act
        var result = await _controller.GetExpenseAnalysis();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetFinancialAnomalies_ReturnsAnomalies()
    {
        // Arrange
        var expectedAnomalies = new FinancialAnomaliesDto
        {
            UnusualTransactions = new List<PaymentAnomalyDto>
            {
                new PaymentAnomalyDto
                {
                    PaymentId = Guid.NewGuid(),
                    Amount = 50000,
                    ZScore = 5.0,
                    PaidAt = DateTime.UtcNow,
                    Method = PaymentMethod.CreditCard
                }
            },
            SuspiciousPaymentPatterns = new List<SuspiciousPatternDto>(),
            NegativeBalanceGroups = new List<GroupNegativeBalanceDto>()
        };

        _adminServiceMock.Setup(s => s.GetFinancialAnomaliesAsync())
            .ReturnsAsync(expectedAnomalies);

        // Act
        var result = await _controller.GetFinancialAnomalies();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Comprehensive Dashboard Tests

    [Fact]
    public async Task GetDashboard_WithAllPeriods_ReturnsCorrectMetrics()
    {
        // Arrange
        var periods = new[] { TimePeriod.Daily, TimePeriod.Weekly, TimePeriod.Monthly, TimePeriod.Quarterly, TimePeriod.Yearly };

        foreach (var period in periods)
        {
            var request = new DashboardRequestDto { Period = period };
            var expectedMetrics = new DashboardMetricsDto
            {
                Users = new UserMetricsDto { TotalUsers = 100, UserGrowthPercentage = 10.5 },
                Groups = new GroupMetricsDto { TotalGroups = 50, GroupGrowthPercentage = 5.2 },
                Vehicles = new VehicleMetricsDto { TotalVehicles = 25 },
                Bookings = new BookingMetricsDto { TotalBookings = 500 },
                Revenue = new RevenueMetricsDto { TotalRevenue = 100000 }
            };

            _adminServiceMock.Setup(s => s.GetDashboardMetricsAsync(request))
                .ReturnsAsync(expectedMetrics);

            // Act
            var result = await _controller.GetDashboard(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
        }
    }

    [Fact]
    public async Task GetDashboard_WithAlertsEnabled_IncludesAlerts()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly, IncludeAlerts = true };
        var expectedMetrics = new DashboardMetricsDto
        {
            Users = new UserMetricsDto(),
            Groups = new GroupMetricsDto(),
            Alerts = new List<AlertDto>
            {
                new AlertDto
                {
                    Type = "Maintenance",
                    Title = "Overdue Maintenance",
                    Message = "5 vehicles require maintenance",
                    Severity = "Warning",
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        _adminServiceMock.Setup(s => s.GetDashboardMetricsAsync(request))
            .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _controller.GetDashboard(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var metrics = okResult!.Value as DashboardMetricsDto;
        metrics!.Alerts.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRecentActivity_WithCustomCount_ReturnsCorrectCount()
    {
        // Arrange
        var count = 50;
        var expectedActivities = Enumerable.Range(1, count).Select(i => new ActivityFeedItemDto
        {
            Id = Guid.NewGuid(),
            Entity = "User",
            Action = "Created",
            UserName = $"User {i}",
            Timestamp = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();

        _adminServiceMock.Setup(s => s.GetRecentActivityAsync(count))
            .ReturnsAsync(expectedActivities);

        // Act
        var result = await _controller.GetRecentActivity(count);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var activities = okResult!.Value as List<ActivityFeedItemDto>;
        activities.Should().HaveCount(count);
    }

    [Fact]
    public async Task GetSystemHealth_ReturnsHealthStatus()
    {
        // Arrange
        var expectedHealth = new SystemHealthDto
        {
            DatabaseConnected = true,
            AllServicesHealthy = true,
            PendingApprovals = 5,
            OverdueMaintenance = 3,
            Disputes = 2,
            SystemErrors = 0,
            LastHealthCheck = DateTime.UtcNow
        };

        _adminServiceMock.Setup(s => s.GetSystemHealthAsync())
            .ReturnsAsync(expectedHealth);

        // Act
        var result = await _controller.GetSystemHealth();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var health = okResult!.Value as SystemHealthDto;
        health!.DatabaseConnected.Should().BeTrue();
    }

    #endregion

    #region Comprehensive User Management Tests

    [Fact]
    public async Task GetUsers_WithAccountStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var request = new UserListRequestDto
        {
            Page = 1,
            PageSize = 20,
            AccountStatus = UserAccountStatus.Suspended
        };

        var expectedResponse = new UserListResponseDto
        {
            Users = new List<UserSummaryDto>
            {
                new UserSummaryDto
                {
                    Id = Guid.NewGuid(),
                    Email = "suspended@test.com",
                    AccountStatus = UserAccountStatus.Suspended
                }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 20,
            TotalPages = 1
        };

        _adminServiceMock.Setup(s => s.GetUsersAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetUsers(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUsers_WithSorting_ReturnsSortedResults()
    {
        // Arrange
        var sortOptions = new[] { "email", "firstname", "lastname", "role", "kycstatus", "createdat" };

        foreach (var sortBy in sortOptions)
        {
            var request = new UserListRequestDto
            {
                Page = 1,
                PageSize = 20,
                SortBy = sortBy,
                SortDirection = "asc"
            };

            _adminServiceMock.Setup(s => s.GetUsersAsync(request))
                .ReturnsAsync(new UserListResponseDto());

            // Act
            var result = await _controller.GetUsers(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
        }
    }

    [Fact]
    public async Task UpdateUserStatus_AllStatusTransitions_WorkCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        SetupCurrentUser(adminUserId);

        var statuses = new[]
        {
            UserAccountStatus.Active,
            UserAccountStatus.Inactive,
            UserAccountStatus.Suspended,
            UserAccountStatus.Banned
        };

        foreach (var status in statuses)
        {
            var request = new UpdateUserStatusDto
            {
                Status = status,
                Reason = $"Testing {status}"
            };

            _adminServiceMock.Setup(s => s.UpdateUserStatusAsync(userId, request, adminUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateUserStatus(userId, request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }
    }

    [Fact]
    public async Task UpdateUserRole_AllRoles_WorkCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        SetupCurrentUser(adminUserId);

        var roles = new[]
        {
            UserRole.CoOwner,
            UserRole.GroupAdmin,
            UserRole.Staff,
            UserRole.SystemAdmin
        };

        foreach (var role in roles)
        {
            var request = new UpdateUserRoleDto
            {
                Role = role,
                Reason = $"Testing {role}"
            };

            _adminServiceMock.Setup(s => s.UpdateUserRoleAsync(userId, request, adminUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateUserRole(userId, request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }
    }

    #endregion

    #region Comprehensive Group Management Tests

    [Fact]
    public async Task GetGroups_WithMemberCountFilters_ReturnsFilteredResults()
    {
        // Arrange
        var request = new GroupListRequestDto
        {
            Page = 1,
            PageSize = 20,
            MinMemberCount = 3,
            MaxMemberCount = 10
        };

        var expectedResponse = new GroupListResponseDto
        {
            Groups = new List<GroupSummaryDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 20,
            TotalPages = 0
        };

        _adminServiceMock.Setup(s => s.GetGroupsAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetGroups(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateGroupStatus_AllStatusTransitions_WorkCorrectly()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        SetupCurrentUser(adminUserId);

        var statuses = new[]
        {
            GroupStatus.Active,
            GroupStatus.Inactive,
            GroupStatus.Dissolved
        };

        foreach (var status in statuses)
        {
            var request = new UpdateGroupStatusDto
            {
                Status = status,
                Reason = $"Testing {status}"
            };

            _adminServiceMock.Setup(s => s.UpdateGroupStatusAsync(groupId, request, adminUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateGroupStatus(groupId, request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }
    }

    [Fact]
    public async Task GetGroupAuditTrail_WithFilters_ReturnsFilteredAuditLogs()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var request = new GroupAuditRequestDto
        {
            Page = 1,
            PageSize = 50,
            Search = "StatusUpdated",
            Action = "StatusUpdated",
            FromDate = DateTime.UtcNow.AddDays(-30),
            ToDate = DateTime.UtcNow
        };

        var expectedResponse = new GroupAuditResponseDto
        {
            Entries = new List<GroupAuditEntryDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 50,
            TotalPages = 0
        };

        _adminServiceMock.Setup(s => s.GetGroupAuditTrailAsync(groupId, request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetGroupAuditTrail(groupId, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task InterveneInGroup_AllInterventionTypes_WorkCorrectly()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        SetupCurrentUser(adminUserId);

        var interventions = new[]
        {
            InterventionType.Freeze,
            InterventionType.Unfreeze,
            InterventionType.Message,
            InterventionType.AppointAdmin
        };

        foreach (var intervention in interventions)
        {
            var request = new GroupInterventionDto
            {
                Type = intervention,
                Message = $"Testing {intervention}",
                Reason = "Test intervention"
            };

            _adminServiceMock.Setup(s => s.InterveneInGroupAsync(groupId, request, adminUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.InterveneInGroup(groupId, request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }
    }

    #endregion

    #region Comprehensive Dispute Management Tests

    [Fact]
    public async Task GetDisputes_WithAllFilters_ReturnsFilteredResults()
    {
        // Arrange
        var request = new DisputeListRequestDto
        {
            Page = 1,
            PageSize = 20,
            Search = "Test",
            Status = DisputeStatus.Open,
            Priority = DisputePriority.High,
            Category = DisputeCategory.Financial,
            AssignedTo = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            SortBy = "priority",
            SortDirection = "desc"
        };

        var expectedResponse = new DisputeListResponseDto
        {
            Disputes = new List<DisputeSummaryDto>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 20,
            TotalPages = 0
        };

        _adminServiceMock.Setup(s => s.GetDisputesAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetDisputes(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDisputeStatistics_ReturnsCompleteStatistics()
    {
        // Arrange
        var expectedStats = new DisputeStatisticsDto
        {
            TotalDisputes = 100,
            OpenDisputes = 20,
            UnderReviewDisputes = 15,
            ResolvedDisputes = 60,
            ClosedDisputes = 5,
            EscalatedDisputes = 0,
            UrgentDisputes = 10,
            HighPriorityDisputes = 20,
            VehicleDamageDisputes = 15,
            LateFeesDisputes = 10,
            UsageDisputes = 20,
            FinancialDisputes = 25,
            OtherDisputes = 30,
            AverageResolutionTimeHours = 48.5,
            DisputesResolvedThisMonth = 25,
            DisputesCreatedThisMonth = 30
        };

        _adminServiceMock.Setup(s => s.GetDisputeStatisticsAsync())
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetDisputeStatistics();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var stats = okResult!.Value as DisputeStatisticsDto;
        stats!.TotalDisputes.Should().Be(100);
        stats.AverageResolutionTimeHours.Should().Be(48.5);
    }

    #endregion

    #region Export Tests

    [Fact]
    public async Task ExportToPdf_ReturnsPdfFile()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF magic number

        _adminServiceMock.Setup(s => s.ExportDashboardToPdfAsync(request))
            .ReturnsAsync(pdfBytes);

        // Act
        var result = await _controller.ExportToPdf(request);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.ContentType.Should().Be("application/pdf");
        fileResult.FileContents.Should().BeEquivalentTo(pdfBytes);
    }

    [Fact]
    public async Task ExportToExcel_ReturnsExcelFile()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };
        var excelBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // Excel magic number

        _adminServiceMock.Setup(s => s.ExportDashboardToExcelAsync(request))
            .ReturnsAsync(excelBytes);

        // Act
        var result = await _controller.ExportToExcel(request);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result as FileContentResult;
        fileResult!.ContentType.Should().Contain("spreadsheet");
        fileResult.FileContents.Should().BeEquivalentTo(excelBytes);
    }

    #endregion

    #region Helper Methods

    private void SetupCurrentUser(Guid userId)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "SystemAdmin")
        }, "Test"));
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #endregion
}


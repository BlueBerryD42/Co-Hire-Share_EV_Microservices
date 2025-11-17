using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Analytics.Api.Tests.Fixtures;
using CoOwnershipVehicle.Analytics.Api.Models;
using CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

namespace CoOwnershipVehicle.Analytics.Api.Tests.UnitTests;

public class AIServiceCostOptimizationTests : IDisposable
{
    private readonly TestDataFixture _fixture;
    private readonly Mock<ILogger<AIService>> _loggerMock;
    private readonly Mock<IGroupServiceClient> _groupServiceClientMock;
    private readonly Mock<IBookingServiceClient> _bookingServiceClientMock;
    private readonly Mock<IPaymentServiceClient> _paymentServiceClientMock;
    private readonly AIService _aiService;

    public AIServiceCostOptimizationTests()
    {
        _fixture = new TestDataFixture();
        _loggerMock = new Mock<ILogger<AIService>>();
        _groupServiceClientMock = new Mock<IGroupServiceClient>();
        _bookingServiceClientMock = new Mock<IBookingServiceClient>();
        _paymentServiceClientMock = new Mock<IPaymentServiceClient>();
        _aiService = new AIService(
            _fixture.AnalyticsContext,
            _groupServiceClientMock.Object,
            _bookingServiceClientMock.Object,
            _paymentServiceClientMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsOptimizationData()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.GroupId.Should().Be(groupId);
        result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.InsufficientData.Should().BeFalse();
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsCostSummary()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Summary.Should().NotBeNull();
        result.Summary.TotalExpenses.Should().BeGreaterThanOrEqualTo(0);
        result.Summary.ExpensesByType.Should().NotBeNull();
        result.Summary.ExpensesByMonth.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsHighCostAreas()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.HighCostAreas.Should().NotBeNull();
        result.HighCostAreas.Should().NotBeEmpty();
        result.HighCostAreas.Should().AllSatisfy(h =>
        {
            h.Category.Should().NotBeNullOrEmpty();
            h.TotalAmount.Should().BeGreaterThanOrEqualTo(0);
            h.Count.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsEfficiencyMetrics()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.EfficiencyMetrics.Should().NotBeNull();
        result.EfficiencyMetrics.CostPerKilometer.Should().BeGreaterThanOrEqualTo(0);
        result.EfficiencyMetrics.CostPerTrip.Should().BeGreaterThanOrEqualTo(0);
        result.EfficiencyMetrics.CostPerMember.Should().BeGreaterThanOrEqualTo(0);
        result.EfficiencyMetrics.CostPerHour.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsBenchmarks()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Benchmarks.Should().NotBeNull();
        result.Benchmarks.IndustryComparison.Should().NotBeNull();
        result.Benchmarks.IndustryComparison.Status.Should().NotBeNullOrEmpty();
        result.Benchmarks.IndustryComparison.VariancePercentage.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsRecommendations()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Recommendations.Should().NotBeNull();
        result.Recommendations.Should().AllSatisfy(r =>
        {
            r.Title.Should().NotBeNullOrEmpty();
            r.Description.Should().NotBeNullOrEmpty();
            r.Category.Should().NotBeNullOrEmpty();
            r.Priority.Should().NotBeNullOrEmpty();
            r.EstimatedSavings.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsPredictions()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Predictions.Should().NotBeNull();
        result.Predictions.NextMonthPrediction.Should().BeGreaterThanOrEqualTo(0);
        result.Predictions.NextQuarterPrediction.Should().BeGreaterThanOrEqualTo(0);
        result.Predictions.ConfidenceScore.Should().BeInRange(0m, 1m);
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsAlerts()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Alerts.Should().NotBeNull();
        result.Alerts.Should().AllSatisfy(a =>
        {
            a.Type.Should().NotBeNullOrEmpty();
            a.Title.Should().NotBeNullOrEmpty();
            a.Description.Should().NotBeNullOrEmpty();
            a.Severity.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsROICalculations()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.ROICalculations.Should().NotBeNull();
        result.ROICalculations.Should().AllSatisfy(r =>
        {
            r.Title.Should().NotBeNullOrEmpty();
            r.Description.Should().NotBeNullOrEmpty();
            r.InvestmentAmount.Should().BeGreaterThanOrEqualTo(0);
            r.Scenario.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task GetCostOptimization_WithNonExistentGroup_ReturnsNull()
    {
        // Arrange
        var nonExistentGroupId = Guid.NewGuid();

        // Act
        var result = await _aiService.GetCostOptimizationAsync(nonExistentGroupId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCostOptimization_WithNoData_ReturnsInsufficientDataFlag()
    {
        // Arrange - create a new group with no expenses or bookings
        var newGroupId = Guid.NewGuid();
        var newGroup = new CoOwnershipVehicle.Domain.Entities.OwnershipGroup
        {
            Id = newGroupId,
            Name = "New Empty Group",
            Description = "Test group with no data",
            Status = CoOwnershipVehicle.Domain.Entities.GroupStatus.Active,
            CreatedBy = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = DateTime.UtcNow
        };

        _fixture.MainContext.OwnershipGroups.Add(newGroup);
        await _fixture.MainContext.SaveChangesAsync();

        // Act
        var result = await _aiService.GetCostOptimizationAsync(newGroupId);

        // Assert
        result.Should().NotBeNull();
        result!.InsufficientData.Should().BeTrue();
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ReturnsCorrectPeriod()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.PeriodStart.Should().BeBefore(DateTime.UtcNow);
        result.PeriodEnd.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        var periodLength = result.PeriodEnd - result.PeriodStart;
        periodLength.TotalDays.Should().BeApproximately(365, 30);
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_DetectsFrequentRepairs()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.HighCostAreas.Should().Contain(h => h.Category == "Frequent Repairs");
    }

    [Fact]
    public async Task GetCostOptimization_WithValidGroup_ProvidesMaintenanceRecommendations()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Recommendations.Should().Contain(r => 
            r.Category == "Preventive Maintenance" || 
            r.Category == "Provider Optimization");
    }
}


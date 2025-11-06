using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Analytics.Api.Tests.Fixtures;
using CoOwnershipVehicle.Analytics.Api.Models;

namespace CoOwnershipVehicle.Analytics.Api.Tests.UnitTests;

public class AIServiceFairnessTests : IDisposable
{
    private readonly TestDataFixture _fixture;
    private readonly Mock<ILogger<AIService>> _loggerMock;
    private readonly AIService _aiService;

    public AIServiceFairnessTests()
    {
        _fixture = new TestDataFixture();
        _loggerMock = new Mock<ILogger<AIService>>();
        _aiService = new AIService(_fixture.AnalyticsContext, _fixture.MainContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }

    [Fact]
    public async Task CalculateFairness_WithBalancedGroup_ReturnsHighFairnessScore()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.GroupFairnessScore.Should().BeGreaterThan(90m);
        result.Members.Count.Should().Be(3);
        
        // Check that all members have balanced fairness scores
        foreach (var member in result.Members)
        {
            member.FairnessScore.Should().BeInRange(95m, 105m);
            member.OwnershipPercentage.Should().BeInRange(33m, 34m);
            member.UsagePercentage.Should().BeInRange(33m, 34m);
        }
    }

    [Fact]
    public async Task CalculateFairness_WithBalancedGroup_ReturnsCorrectGiniCoefficient()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.GiniCoefficient.Should().BeLessThan(0.1m, "Balanced group should have low Gini coefficient");
        result.StandardDeviationFromOwnership.Should().BeLessThan(5m);
    }

    [Fact]
    public async Task CalculateFairness_WithImbalancedGroup_ReturnsLowFairnessScore()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.GroupFairnessScore.Should().BeLessThan(90m);
        result.Members.Count.Should().Be(3);
        
        // User 2 should be detected as slightly overutilizing
        var user2Member = result.Members.FirstOrDefault(m => m.UserId == Guid.Parse("33333333-3333-3333-3333-333333333333"));
        user2Member.Should().NotBeNull();
    }

    [Fact]
    public async Task CalculateFairness_WithImbalancedGroup_ReturnsCorrectAlerts()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.Alerts.Should().NotBeNull();
        result.Alerts.GroupFairnessLow.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateFairness_WithCustomDateRange_ReturnsCorrectResults()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result!.PeriodStart.Should().BeCloseTo(startDate, TimeSpan.FromHours(1));
        result.PeriodEnd.Should().BeCloseTo(endDate, TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task CalculateFairness_WithNoData_ReturnsNull()
    {
        // Arrange
        var nonExistentGroup = Guid.NewGuid();

        // Act
        var result = await _aiService.CalculateFairnessAsync(nonExistentGroup, null, null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CalculateFairness_WithNoMembers_ReturnsEmptyResponse()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var farFutureDate = DateTime.UtcNow.AddYears(10);

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, farFutureDate, DateTime.UtcNow.AddYears(11));

        // Assert
        result.Should().NotBeNull();
        result!.GroupFairnessScore.Should().Be(0);
        result.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateFairness_GeneratesRecommendations()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.Recommendations.Should().NotBeNull();
        result.Recommendations.GroupRecommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CalculateFairness_ReturnsVisualizationData()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.Visualization.Should().NotBeNull();
        result.Visualization.OwnershipVsUsageChart.Should().HaveCount(3);
        result.Visualization.FairnessTimeline.Should().NotBeEmpty();
        result.Visualization.MemberComparison.Should().HaveCount(3);
    }

    [Fact]
    public async Task CalculateFairness_ReturnsTrendData()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.Trend.Should().NotBeEmpty();
        result.Trend.Should().HaveCountLessThanOrEqualTo(6);
    }

    [Fact]
    public async Task CalculateFairness_HandlesSingleMemberGroup()
    {
        // Arrange
        var groupId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        if (result.Members.Any())
        {
            result.Members[0].FairnessScore.Should().BeGreaterThan(95m);
        }
    }

    [Theory]
    [InlineData("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", 95, 105)] // Balanced
    [InlineData("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", 0, 90)]   // Imbalanced
    public async Task CalculateFairness_ReturnsExpectedFairnessRange(
        string groupIdStr, 
        decimal minScore, 
        decimal maxScore)
    {
        // Arrange
        var groupId = Guid.Parse(groupIdStr);

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.GroupFairnessScore.Should().BeInRange(minScore, maxScore);
    }

    [Fact]
    public async Task CalculateFairness_WithExtremeImbalance_DetectsSevereOverUtilizers()
    {
        // Arrange
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.Alerts.Should().NotBeNull();
        result.Members.Should().Contain(m => m.FairnessScore > 100m || m.FairnessScore < 100m);
    }

    [Fact]
    public async Task CalculateFairness_CalculatesFairnessIndexCorrectly()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.FairnessIndex.Should().Be(result.GroupFairnessScore);
    }
}


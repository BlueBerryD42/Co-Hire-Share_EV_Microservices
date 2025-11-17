using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Analytics.Api.Tests.Fixtures;
using CoOwnershipVehicle.Analytics.Api.Models;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

namespace CoOwnershipVehicle.Analytics.Api.Tests.UnitTests;

public class AIServiceUsagePredictionTests : IDisposable
{
    private readonly TestDataFixture _fixture;
    private readonly Mock<ILogger<AIService>> _loggerMock;
    private readonly Mock<IGroupServiceClient> _groupServiceClientMock;
    private readonly Mock<IBookingServiceClient> _bookingServiceClientMock;
    private readonly Mock<IPaymentServiceClient> _paymentServiceClientMock;
    private readonly AIService _aiService;

    public AIServiceUsagePredictionTests()
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
    public async Task GetUsagePredictions_WithValidGroup_ReturnsPredictions()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.GroupId.Should().Be(groupId);
        result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetUsagePredictions_WithValidGroup_Returns30DayPredictions()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Next30Days.Should().HaveCount(30);
        result.Next30Days.Should().OnlyContain(d => d.Date > DateTime.UtcNow);
    }

    [Fact]
    public async Task GetUsagePredictions_WithValidGroup_ReturnsPeakHours()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.PeakHours.Should().NotBeEmpty();
        result.PeakHours.Should().AllSatisfy(h => 
        {
            h.Hour.Should().BeInRange(0, 23);
            h.RelativeLoad.Should().BeInRange(0m, 1m);
            h.Confidence.Should().BeInRange(0m, 1m);
        });
    }

    [Fact]
    public async Task GetUsagePredictions_WithValidGroup_ReturnsMemberLikelihoods()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.MemberLikelyUsage.Should().NotBeEmpty();
        result.MemberLikelyUsage.Should().AllSatisfy(m =>
        {
            m.DayOfWeek.Should().BeInRange(0, 6);
            m.Hour.Should().BeInRange(0, 23);
            m.Likelihood.Should().BeInRange(0m, 1m);
        });
    }

    [Fact]
    public async Task GetUsagePredictions_WithValidGroup_ReturnsInsights()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Insights.Should().BeAssignableTo<IEnumerable<PredictionInsight>>();
    }

    [Fact]
    public async Task GetUsagePredictions_WithValidGroup_ReturnsAnomalies()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Anomalies.Should().BeAssignableTo<IEnumerable<PredictionAnomaly>>();
    }

    [Fact]
    public async Task GetUsagePredictions_WithValidGroup_ReturnsBottlenecks()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Bottlenecks.Should().BeAssignableTo<IEnumerable<BottleneckPrediction>>();
    }

    [Fact]
    public async Task GetUsagePredictions_WithValidGroup_ReturnsRecommendations()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Recommendations.Should().NotBeEmpty();
        result.Recommendations.Should().AllSatisfy(r =>
        {
            r.Action.Should().NotBeNullOrEmpty();
            r.Rationale.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task GetUsagePredictions_WithInsufficientHistory_ReturnsInsufficientHistoryFlag()
    {
        // Arrange - create a new group with no history
        var newGroupId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();
        var newUser = new CoOwnershipVehicle.Domain.Entities.User
        {
            Id = newUserId,
            Email = "newuser@test.com",
            FirstName = "New",
            LastName = "User",
            KycStatus = CoOwnershipVehicle.Domain.Entities.KycStatus.Approved,
            Role = CoOwnershipVehicle.Domain.Entities.UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow
        };

        var newGroup = new OwnershipGroup
        {
            Id = newGroupId,
            Name = "Insufficient History Group",
            Status = GroupStatus.Active,
            CreatedBy = newUserId,
            CreatedAt = DateTime.UtcNow,
            Members = new List<GroupMember>
            {
                new GroupMember
                {
                    UserId = newUserId,
                    SharePercentage = 1.0m,
                    RoleInGroup = GroupRole.Member,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        _fixture.MainContext.Users.Add(newUser);
        _fixture.MainContext.OwnershipGroups.Add(newGroup);
        await _fixture.MainContext.SaveChangesAsync();

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(newGroupId);

        // Assert
        result.Should().NotBeNull();
        result!.InsufficientHistory.Should().BeTrue();
    }

    [Fact]
    public async Task GetUsagePredictions_WithNonExistentGroup_ReturnsNull()
    {
        // Arrange
        var nonExistentGroupId = Guid.NewGuid();

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(nonExistentGroupId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUsagePredictions_ReturnsDayPredictionsWithConfidence()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.Next30Days.Should().NotBeEmpty();
        result.Next30Days.Should().AllSatisfy(d =>
        {
            d.ExpectedUsageHours.Should().BeGreaterThanOrEqualTo(0m);
            d.Confidence.Should().BeInRange(0.4m, 1m);
        });
    }

    [Fact]
    public async Task GetUsagePredictions_DayPredictionsAreInOrder()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        
        for (int i = 0; i < result!.Next30Days.Count - 1; i++)
        {
            result.Next30Days[i].Date.Should().BeBefore(result.Next30Days[i + 1].Date);
        }
    }

    [Fact]
    public async Task GetUsagePredictions_ReturnsHistoricalData()
    {
        // Arrange
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.HistoryStart.Should().BeBefore(DateTime.UtcNow);
        result.HistoryEnd.Should().BeOnOrBefore(DateTime.UtcNow);
    }
}


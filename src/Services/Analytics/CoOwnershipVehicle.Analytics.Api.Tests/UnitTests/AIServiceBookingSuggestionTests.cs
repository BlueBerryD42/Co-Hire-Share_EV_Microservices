using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Analytics.Api.Tests.Fixtures;
using CoOwnershipVehicle.Analytics.Api.Models;

namespace CoOwnershipVehicle.Analytics.Api.Tests.UnitTests;

public class AIServiceBookingSuggestionTests : IDisposable
{
    private readonly TestDataFixture _fixture;
    private readonly Mock<ILogger<AIService>> _loggerMock;
    private readonly AIService _aiService;

    public AIServiceBookingSuggestionTests()
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
    public async Task SuggestBookingTimes_WithValidRequest_ReturnsSuggestions()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DurationMinutes = 120,
            PreferredDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Suggestions.Should().NotBeEmpty();
        result.Suggestions.Should().HaveCountLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task SuggestBookingTimes_WithValidRequest_ReturnsTop5Suggestions()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DurationMinutes = 120
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Suggestions.Should().NotBeEmpty();
        result.Suggestions.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task SuggestBookingTimes_WithPreferredDate_ReturnsSuggestionsNearPreferredDate()
    {
        // Arrange
        var preferredDate = DateTime.UtcNow.AddDays(2);
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DurationMinutes = 120,
            PreferredDate = preferredDate
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Suggestions.Should().NotBeEmpty();
        result.Suggestions.Should().OnlyContain(s => s.Start >= preferredDate.AddDays(-7) && s.Start <= preferredDate.AddDays(7));
    }

    [Fact]
    public async Task SuggestBookingTimes_ReturnsSuggestionsWithReasons()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DurationMinutes = 120
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Suggestions.Should().NotBeEmpty();
        foreach (var suggestion in result.Suggestions)
        {
            suggestion.Reasons.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task SuggestBookingTimes_ReturnsSuggestionsWithConfidence()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DurationMinutes = 120
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Suggestions.Should().NotBeEmpty();
        foreach (var suggestion in result.Suggestions)
        {
            suggestion.Confidence.Should().BeInRange(0m, 1m);
        }
    }

    [Fact]
    public async Task SuggestBookingTimes_ReturnsSuggestionsSortedByConfidence()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DurationMinutes = 120
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Suggestions.Should().NotBeEmpty();
        
        // Verify suggestions are sorted by confidence descending
        for (int i = 0; i < result.Suggestions.Count - 1; i++)
        {
            result.Suggestions[i].Confidence.Should().BeGreaterThanOrEqualTo(result.Suggestions[i + 1].Confidence);
        }
    }

    [Fact]
    public async Task SuggestBookingTimes_WithInvalidGroupId_ReturnsNull()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.NewGuid(), // Non-existent group
            DurationMinutes = 120
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestBookingTimes_WithMinimumDuration_RespectsDuration()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DurationMinutes = 30
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Suggestions.Should().NotBeEmpty();
        foreach (var suggestion in result.Suggestions)
        {
            var duration = suggestion.End - suggestion.Start;
            duration.TotalMinutes.Should().BeGreaterThanOrEqualTo(30);
        }
    }

    [Fact]
    public async Task SuggestBookingTimes_ForUnderutilizer_ProvidesFavorableSuggestions()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            DurationMinutes = 120
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Suggestions.Should().NotBeEmpty();
        // Underutilizers should get reasonable suggestions
        result.Suggestions.First().Confidence.Should().BeGreaterThan(0.5m);
    }

    [Fact]
    public async Task SuggestBookingTimes_ReturnsSuggestionsWithin7Days()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DurationMinutes = 120,
            PreferredDate = DateTime.UtcNow
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Suggestions.Should().NotBeEmpty();
        
        var maxDate = DateTime.UtcNow.AddDays(7);
        foreach (var suggestion in result.Suggestions)
        {
            suggestion.Start.Should().BeBefore(maxDate);
            suggestion.End.Should().BeBefore(maxDate.AddHours(8));
        }
    }

    [Fact]
    public async Task SuggestBookingTimes_WithGroupData_ReturnsAppropriateSuggestions()
    {
        // Arrange
        var request = new SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DurationMinutes = 120
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.GroupId.Should().Be(request.GroupId);
        result.UserId.Should().Be(request.UserId);
        result.Suggestions.Should().NotBeEmpty();
    }
}


using CoOwnershipVehicle.Booking.Api.Configuration;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Booking.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CoOwnershipVehicle.Booking.Api.Tests;

public class BookingServiceTests
{
    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<ILogger<BookingService>> _logger = new();
    private readonly IOptions<TripPricingOptions> _pricingOptions = Options.Create(new TripPricingOptions());

    [Fact]
    public async Task GetUserBookingHistoryAsync_WhenLimitIsInvalid_UsesDefaultAndMapsCheckIns()
    {
        var userId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var checkOutTime = DateTime.UtcNow.AddDays(-2);
        var checkInTime = DateTime.UtcNow.AddDays(-2).AddHours(4);
        var booking = new Domain.Entities.Booking
        {
            Id = bookingId,
            UserId = userId,
            VehicleId = Guid.NewGuid(),
            GroupId = Guid.NewGuid(),
            StartAt = checkOutTime.AddHours(-1),
            EndAt = checkInTime,
            Vehicle = new Vehicle { Model = "Model S", PlateNumber = "ABC123" },
            Group = new OwnershipGroup { Name = "Group A" },
            User = new User { FirstName = "John", LastName = "Doe" },
            CheckIns = new List<CheckIn>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    UserId = userId,
                    VehicleId = Guid.NewGuid(),
                    Type = CheckInType.CheckOut,
                    Odometer = 1000,
                    CheckInTime = checkOutTime,
                    Photos = new List<CheckInPhoto>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            PhotoUrl = "http://photo/1",
                            Type = PhotoType.Exterior
                        }
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    UserId = userId,
                    VehicleId = Guid.NewGuid(),
                    Type = CheckInType.CheckIn,
                    Odometer = 1100,
                    CheckInTime = checkInTime,
                    Photos = new List<CheckInPhoto>()
                }
            }
        };

        _bookingRepository
            .Setup(repo => repo.GetUserBookingHistoryAsync(
                userId,
                It.IsAny<DateTime>(),
                It.Is<int>(limit => limit == 20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Entities.Booking> { booking });

        var service = new BookingService(_bookingRepository.Object, _logger.Object, _pricingOptions);

        var history = await service.GetUserBookingHistoryAsync(userId, 0);

        history.Should().HaveCount(1);
        var entry = history.First();
        entry.Booking.Id.Should().Be(bookingId);
        entry.CheckIns.Should().HaveCount(2);
        entry.CheckIns.First().Type.Should().Be(CheckInType.CheckOut);
        entry.CheckIns.Last().Type.Should().Be(CheckInType.CheckIn);
        entry.CheckIns.First().Photos.Should().HaveCount(1);

        _bookingRepository.Verify(repo => repo.GetUserBookingHistoryAsync(
            userId,
            It.IsAny<DateTime>(),
            It.Is<int>(limit => limit == 20),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}


using CoOwnershipVehicle.Booking.Api.Configuration;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Booking.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using BookingEntity = CoOwnershipVehicle.Domain.Entities.Booking;
using CheckInEntity = CoOwnershipVehicle.Domain.Entities.CheckIn;

namespace CoOwnershipVehicle.Booking.Api.Tests;

public class CheckInServiceTests
{
    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<ICheckInRepository> _checkInRepository = new();
    private readonly Mock<ILogger<CheckInService>> _logger = new();
    private readonly IOptions<TripPricingOptions> _pricingOptions = Options.Create(new TripPricingOptions
    {
        CostPerKm = 1.25m,
        MinimumFee = 0m
    });

    [Fact]
    public async Task StartTripAsync_WhenPreviousCheckoutNotClosed_Throws()
    {
        var bookingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var booking = new BookingEntity
        {
            Id = bookingId,
            VehicleId = Guid.NewGuid(),
            UserId = userId,
            EndAt = DateTime.UtcNow.AddHours(1),
            VehicleStatus = VehicleStatus.InUse
        };

        _bookingRepository
            .Setup(repo => repo.GetBookingWithVehicleAndUserAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        _checkInRepository
            .Setup(repo => repo.GetLatestAsync(bookingId, CheckInType.CheckOut, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                CheckInTime = DateTime.UtcNow.AddMinutes(-5),
                Odometer = 1000,
                Type = CheckInType.CheckOut
            });

        _checkInRepository
            .Setup(repo => repo.GetLatestAsync(bookingId, CheckInType.CheckIn, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInEntity?)null);

        var service = CreateService();
        var request = new StartTripDto
        {
            BookingId = bookingId,
            OdometerReading = 1500,
            ClientTimestamp = DateTime.UtcNow
        };

        var act = async () => await service.StartTripAsync(request, userId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Trip already in progress*");

        _checkInRepository.Verify(repo => repo.AddAsync(It.IsAny<CheckInEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartTripAsync_WhenNoPendingCheckout_PersistsNewCheckout()
    {
        var bookingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var booking = new BookingEntity
        {
            Id = bookingId,
            VehicleId = Guid.NewGuid(),
            UserId = userId,
            EndAt = DateTime.UtcNow.AddHours(2),
            VehicleStatus = VehicleStatus.Available
        };

        _bookingRepository
            .Setup(repo => repo.GetBookingWithVehicleAndUserAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        _checkInRepository
            .Setup(repo => repo.GetLatestAsync(bookingId, CheckInType.CheckOut, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                CheckInTime = DateTime.UtcNow.AddHours(-2),
                Odometer = 900,
                Type = CheckInType.CheckOut
            });

        _checkInRepository
            .Setup(repo => repo.GetLatestAsync(bookingId, CheckInType.CheckIn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckInEntity
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                CheckInTime = DateTime.UtcNow.AddHours(-1),
                Odometer = 950,
                Type = CheckInType.CheckIn
            });

        CheckInEntity? persistedEntity = null;
        _checkInRepository
            .Setup(repo => repo.AddAsync(It.IsAny<CheckInEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CheckInEntity, CancellationToken>((entity, _) => persistedEntity = entity)
            .Returns(Task.CompletedTask);

        _checkInRepository
            .Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _bookingRepository
            .Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var request = new StartTripDto
        {
            BookingId = bookingId,
            OdometerReading = 1800,
            Notes = "Starting trip",
            ClientTimestamp = DateTime.UtcNow
        };

        var result = await service.StartTripAsync(request, userId, CancellationToken.None);

        persistedEntity.Should().NotBeNull();
        persistedEntity!.Type.Should().Be(CheckInType.CheckOut);
        persistedEntity.Odometer.Should().Be(1800);

        result.Type.Should().Be(CheckInType.CheckOut);
        result.Odometer.Should().Be(1800);
        booking.VehicleStatus.Should().Be(VehicleStatus.InUse);

        _checkInRepository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _bookingRepository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartTripAsync_WhenBookingAlreadyEnded_Throws()
    {
        var bookingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var booking = new BookingEntity
        {
            Id = bookingId,
            VehicleId = Guid.NewGuid(),
            UserId = userId,
            EndAt = DateTime.UtcNow.AddDays(-1),
            VehicleStatus = VehicleStatus.Available
        };

        _bookingRepository
            .Setup(repo => repo.GetBookingWithVehicleAndUserAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var service = CreateService();
        var request = new StartTripDto
        {
            BookingId = bookingId,
            OdometerReading = 1200
        };

        var act = async () => await service.StartTripAsync(request, userId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This booking has already ended*");

        _checkInRepository.Verify(repo => repo.AddAsync(It.IsAny<CheckInEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private CheckInService CreateService()
    {
        return new CheckInService(
            _bookingRepository.Object,
            _checkInRepository.Object,
            _logger.Object,
            _pricingOptions);
    }
}

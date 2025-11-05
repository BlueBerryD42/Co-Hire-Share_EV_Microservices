using System;
using System.Collections.Generic;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class CheckInHistoryFilterDto
{
    public Guid? VehicleId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public CheckInType? Type { get; set; }
}

public class CheckInRecordMetricsDto
{
    public DateTime Timestamp { get; set; }
    public double? MinutesFromBookingStart { get; set; }
    public double? MinutesUntilBookingEnd { get; set; }
    public double? MinutesSincePreviousEvent { get; set; }
    public int Odometer { get; set; }
}

public class CheckInRecordDetailDto
{
    public CheckInDto Record { get; set; } = new();
    public IReadOnlyDictionary<PhotoType, IReadOnlyList<CheckInPhotoDto>> PhotosByCategory { get; set; } =
        new Dictionary<PhotoType, IReadOnlyList<CheckInPhotoDto>>();
    public IReadOnlyList<DamageReportDto> DamageReports { get; set; } = Array.Empty<DamageReportDto>();
    public CheckInRecordMetricsDto Metrics { get; set; } = new();
}

public class PhotoGalleryDto
{
    public IReadOnlyList<PhotoGalleryGroupDto> Groups { get; set; } = Array.Empty<PhotoGalleryGroupDto>();
}

public class PhotoGalleryGroupDto
{
    public CheckInType CheckInType { get; set; }
    public PhotoType PhotoType { get; set; }
    public IReadOnlyList<CheckInPhotoDto> Photos { get; set; } = Array.Empty<CheckInPhotoDto>();
}

public class TimelineEventDto
{
    public Guid? CheckInId { get; set; }
    public Guid? BookingId { get; set; }
    public Guid? DamageReportId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CheckInTripStatisticsDto
{
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public DateTime? ActualCheckOut { get; set; }
    public DateTime? ActualCheckIn { get; set; }
    public double PlannedDurationMinutes { get; set; }
    public double? ActualDurationMinutes { get; set; }
    public int? StartOdometer { get; set; }
    public int? EndOdometer { get; set; }
    public int? TripDistance { get; set; }
    public double? AverageSpeedKph { get; set; }
    public double? LateReturnMinutes { get; set; }
    public decimal? LateFeeAmount { get; set; }
}

public class PhotoComparisonDto
{
    public PhotoType PhotoType { get; set; }
    public IReadOnlyList<CheckInPhotoDto> CheckOutPhotos { get; set; } = Array.Empty<CheckInPhotoDto>();
    public IReadOnlyList<CheckInPhotoDto> CheckInPhotos { get; set; } = Array.Empty<CheckInPhotoDto>();
}

public class ConditionChangeDto
{
    public string Field { get; set; } = string.Empty;
    public string? CheckOutValue { get; set; }
    public string? CheckInValue { get; set; }
    public string? Highlight { get; set; }
    public IReadOnlyList<DamageReportDto> RelatedDamageReports { get; set; } = Array.Empty<DamageReportDto>();
}

public class BookingCheckInHistoryDto
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public string VehicleDisplayName { get; set; } = string.Empty;
    public string BookingOwnerName { get; set; } = string.Empty;
    public IReadOnlyList<CheckInRecordDetailDto> Records { get; set; } = Array.Empty<CheckInRecordDetailDto>();
    public CheckInTripStatisticsDto TripStatistics { get; set; } = new();
    public PhotoGalleryDto PhotoGallery { get; set; } = new();
    public IReadOnlyList<LateReturnFeeDto> LateReturnFees { get; set; } = Array.Empty<LateReturnFeeDto>();
    public IReadOnlyList<TimelineEventDto> Timeline { get; set; } = Array.Empty<TimelineEventDto>();
}

public class CheckInComparisonDto
{
    public Guid BookingId { get; set; }
    public CheckInRecordDetailDto? CheckOut { get; set; }
    public CheckInRecordDetailDto? CheckIn { get; set; }
    public CheckInTripStatisticsDto TripStatistics { get; set; } = new();
    public IReadOnlyList<PhotoComparisonDto> PhotoComparisons { get; set; } = Array.Empty<PhotoComparisonDto>();
    public IReadOnlyList<ConditionChangeDto> ConditionChanges { get; set; } = Array.Empty<ConditionChangeDto>();
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.DTOs
{
    public class CreateRecurringBookingRequest
    {
        [Required]
        public Guid VehicleId { get; set; }
        [Required]
        public Guid GroupId { get; set; }
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public RecurrencePattern Pattern { get; set; }
        public List<DayOfWeek>? DaysOfWeek { get; set; }
        [Required]
        public TimeSpan StartTime { get; set; }
        [Required]
        public TimeSpan EndTime { get; set; }
        [Required]
        public DateOnly RecurrenceStartDate { get; set; }
        public DateOnly? RecurrenceEndDate { get; set; }
        public string? Notes { get; set; }
        public string? Purpose { get; set; }
        public string? TimeZoneId { get; set; }
    }

    public class RecurringBookingResponse
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public RecurrencePattern Pattern { get; set; }
        public List<DayOfWeek>? DaysOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public DateOnly RecurrenceStartDate { get; set; }
        public DateOnly? RecurrenceEndDate { get; set; }
        public RecurringBookingStatus Status { get; set; }
        public string? Notes { get; set; }
        public string? Purpose { get; set; }
        public string? CancellationReason { get; set; }
        public string? TimeZoneId { get; set; }
    }
}
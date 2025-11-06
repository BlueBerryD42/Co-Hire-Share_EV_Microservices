
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities
{
    public class RecurringBooking : BaseEntity
    {
        public DateTime? LastGeneratedUntilUtc { get; set; }
        public DateTime? LastGenerationRunAtUtc { get; set; }
        public DateTime? PausedUntilUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public Guid VehicleId { get; set; }
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public RecurrencePattern Pattern { get; set; }
        public int Interval { get; set; }
        public int? DaysOfWeekMask { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public DateOnly RecurrenceStartDate { get; set; }
        public DateOnly? RecurrenceEndDate { get; set; }
        public RecurringBookingStatus Status { get; set; } = RecurringBookingStatus.Active;
        public string? Notes { get; set; }
        public string? Purpose { get; set; }
        public string? CancellationReason { get; set; }
        public string? TimeZoneId { get; set; }


        // Navigation property for the generated bookings
        public virtual ICollection<Booking> GeneratedBookings { get; set; } = new List<Booking>();

        // Navigation properties
        public virtual Vehicle Vehicle { get; set; } = null!;
        public virtual OwnershipGroup Group { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }

    public enum RecurrencePattern
    {
        Daily,
        Weekly,
        Monthly
    }

    public enum RecurringBookingStatus
    {
        Active,
        Paused,
        Ended
    }
}
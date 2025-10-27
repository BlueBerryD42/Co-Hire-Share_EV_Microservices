
using System;
using System.Collections.Generic;

namespace CoOwnershipVehicle.Vehicle.Api.Services
{
    public class BookingConflictDto
    {
        public Guid VehicleId { get; set; }
        public DateTime RequestedStartAt { get; set; }
        public DateTime RequestedEndAt { get; set; }
        public bool HasConflicts { get; set; }
        public List<BookingDto> ConflictingBookings { get; set; } = new();
    }

    public class BookingDto
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public Guid UserId { get; set; }
        public string UserFirstName { get; set; }
        public string UserLastName { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public BookingStatus Status { get; set; }
        public int Priority { get; set; }
        public bool IsEmergency { get; set; }
    }

    public enum BookingStatus
    {
        Pending = 0,
        PendingApproval = 1,
        Confirmed = 2,
        InProgress = 3,
        Completed = 4,
        Cancelled = 5,
        NoShow = 6
    }
}

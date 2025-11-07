using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Vehicle.Api.DTOs
{
    public class UpdateVehicleStatusDto
    {
        public VehicleStatus Status { get; set; }
        
        /// <summary>
        /// Optional: Start time for status change (e.g., when vehicle becomes unavailable)
        /// Used for checking booking conflicts
        /// </summary>
        public DateTime? From { get; set; }
        
        /// <summary>
        /// Optional: End time for status change (e.g., when vehicle becomes available again)
        /// Used for checking booking conflicts
        /// </summary>
        public DateTime? To { get; set; }
    }
}

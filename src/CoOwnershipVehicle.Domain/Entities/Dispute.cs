using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities
{
    public class Dispute : BaseEntity
    {
        [Required]
        public Guid GroupId { get; set; }
        public virtual OwnershipGroup Group { get; set; }

        [Required]
        public Guid ReportedBy { get; set; }
        public virtual User Reporter { get; set; }

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; }

        [Required]
        public DisputeCategory Category { get; set; }

        [Required]
        public DisputePriority Priority { get; set; }

        [Required]
        public DisputeStatus Status { get; set; }

        public Guid? AssignedTo { get; set; }
        public virtual User AssignedStaff { get; set; }

        [MaxLength(2000)]
        public string? Resolution { get; set; }

        public Guid? ResolvedBy { get; set; }
        public virtual User Resolver { get; set; }

        public DateTime? ResolvedAt { get; set; }

        // Navigation properties
        public virtual ICollection<DisputeComment> Comments { get; set; } = new List<DisputeComment>();
    }

    public enum DisputeCategory
    {
        VehicleDamage = 0,
        LateFees = 1,
        Usage = 2,
        Financial = 3,
        Other = 4
    }

    public enum DisputePriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Urgent = 3
    }

    public enum DisputeStatus
    {
        Open = 0,
        UnderReview = 1,
        Resolved = 2,
        Closed = 3,
        Escalated = 4
    }
}


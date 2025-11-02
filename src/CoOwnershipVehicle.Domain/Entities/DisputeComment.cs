using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities
{
    public class DisputeComment : BaseEntity
    {
        [Required]
        public Guid DisputeId { get; set; }
        public virtual Dispute Dispute { get; set; }

        [Required]
        public Guid CommentedBy { get; set; }
        public virtual User Commenter { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Comment { get; set; }

        public bool IsInternal { get; set; } = false; // Internal staff comments
    }
}






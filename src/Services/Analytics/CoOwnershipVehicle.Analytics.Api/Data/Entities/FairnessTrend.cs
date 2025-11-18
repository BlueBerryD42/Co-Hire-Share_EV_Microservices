using CoOwnershipVehicle.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Analytics.Api.Data.Entities;

public class FairnessTrend : BaseEntity
{
	public Guid GroupId { get; set; }
	public DateTime PeriodStart { get; set; }
	public DateTime PeriodEnd { get; set; }
	[Column(TypeName = "decimal(5,2)")]
	public decimal GroupFairnessScore { get; set; }
}














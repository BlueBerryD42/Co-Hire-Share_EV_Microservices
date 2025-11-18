using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class VehicleDto
{
    public Guid Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Color { get; set; }
    public VehicleStatus Status { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public int Odometer { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateVehicleDto
{
    [Required]
    [StringLength(17, MinimumLength = 17, ErrorMessage = "VIN must be exactly 17 characters.")]
    public string Vin { get; set; } = string.Empty;
    
    [Required]
    [StringLength(20)]
    public string PlateNumber { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Model { get; set; } = string.Empty;
    
    [Range(1900, 2030)]
    public int Year { get; set; }
    
    public string? Color { get; set; }
    
    [Range(0, int.MaxValue)]
    public int Odometer { get; set; }
    
    public Guid? GroupId { get; set; }
}

public class UpdateVehicleDto
{
    public string? Color { get; set; }
    public VehicleStatus? Status { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public int? Odometer { get; set; }
    public Guid? GroupId { get; set; }
}

public class VehicleAvailabilityDto
{
    public Guid VehicleId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool IsAvailable { get; set; }
    public List<BookingDto> ConflictingBookings { get; set; } = new();
}


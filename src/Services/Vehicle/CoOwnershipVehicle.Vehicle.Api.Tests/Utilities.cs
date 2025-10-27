
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using System;
using System.Linq;

namespace CoOwnershipVehicle.Vehicle.Api.Tests
{
    public static class Utilities
    {
        public static void InitializeDbForTests(VehicleDbContext db)
        {
            db.Vehicles.RemoveRange(db.Vehicles);
            db.SaveChanges();

            db.Vehicles.Add(new Vehicle
            {
                Id = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Vin = "VIN123456789012345",
                PlateNumber = "PLATE1",
                Model = "Model S",
                Year = 2020,
                Color = "Red",
                Status = VehicleStatus.Available,
                Odometer = 10000,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Vehicles.Add(new Vehicle
            {
                Id = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa8"),
                Vin = "VIN123456789012346",
                PlateNumber = "PLATE2",
                Model = "Model 3",
                Year = 2021,
                Color = "Blue",
                Status = VehicleStatus.InUse,
                Odometer = 20000,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }
}

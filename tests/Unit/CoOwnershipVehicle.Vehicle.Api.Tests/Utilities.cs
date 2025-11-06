
using CoOwnershipVehicle.Vehicle.Api.Data;
using System;
using System.Linq;
using VehicleEntity = CoOwnershipVehicle.Domain.Entities.Vehicle;
using static CoOwnershipVehicle.Domain.Entities.VehicleStatus;

namespace CoOwnershipVehicle.Vehicle.Api.Tests
{
    public static class Utilities
    {
        public static void InitializeDbForTests(VehicleDbContext db)
        {
            db.Vehicles.RemoveRange(db.Vehicles);
            db.SaveChanges();

            db.Vehicles.Add(new VehicleEntity
            {
                Id = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                Vin = "VIN123456789012345",
                PlateNumber = "PLATE1",
                Model = "Model S",
                Year = 2020,
                Color = "Red",
                Status = Available,
                Odometer = 10000,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.Vehicles.Add(new VehicleEntity
            {
                Id = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa8"),
                Vin = "VIN123456789012346",
                PlateNumber = "PLATE2",
                Model = "Model 3",
                Year = 2021,
                Color = "Blue",
                Status = InUse,
                Odometer = 20000,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }
}

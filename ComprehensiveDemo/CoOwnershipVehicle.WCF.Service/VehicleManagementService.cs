using CoreWCF;
using CoOwnershipVehicle.WCF.Contracts;

namespace CoOwnershipVehicle.WCF.Service
{
    /// <summary>
    /// WCF Service Implementation for Vehicle Management
    /// Implements the shared contracts from CoOwnershipVehicle.WCF.Contracts
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class VehicleManagementService : IVehicleManagementService
    {
        // In-memory data store for demo purposes
        private readonly List<VehicleDTO> _vehicles;

        public VehicleManagementService()
        {
            _vehicles = new List<VehicleDTO>
            {
                new VehicleDTO
                {
                    Id = 1,
                    Brand = "Toyota",
                    Model = "Camry",
                    Year = 2022,
                    LicensePlate = "ABC-123",
                    Status = "Available",
                    PricePerDay = 50.00m
                },
                new VehicleDTO
                {
                    Id = 2,
                    Brand = "Honda",
                    Model = "Civic",
                    Year = 2021,
                    LicensePlate = "XYZ-789",
                    Status = "Available",
                    PricePerDay = 45.00m
                },
                new VehicleDTO
                {
                    Id = 3,
                    Brand = "Ford",
                    Model = "Mustang",
                    Year = 2023,
                    LicensePlate = "DEF-456",
                    Status = "Rented",
                    PricePerDay = 90.00m
                },
                new VehicleDTO
                {
                    Id = 4,
                    Brand = "Tesla",
                    Model = "Model 3",
                    Year = 2024,
                    LicensePlate = "GHI-789",
                    Status = "Available",
                    PricePerDay = 120.00m
                },
                new VehicleDTO
                {
                    Id = 5,
                    Brand = "BMW",
                    Model = "X5",
                    Year = 2023,
                    LicensePlate = "JKL-012",
                    Status = "Available",
                    PricePerDay = 110.00m
                }
            };
        }

        public async Task<string> GetServiceInfoAsync()
        {
            await Task.CompletedTask; // Simulate async operation
            return $"Co-Ownership Vehicle WCF Service - Running on CoreWCF (.NET 8) - Total Vehicles: {_vehicles.Count}";
        }

        public async Task<List<VehicleDTO>> GetAvailableVehiclesAsync()
        {
            await Task.CompletedTask; // Simulate async operation
            return _vehicles.Where(v => v.Status == "Available").ToList();
        }
    }
}

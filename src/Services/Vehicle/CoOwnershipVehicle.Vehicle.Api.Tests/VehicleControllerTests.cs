
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Threading.Tasks;
using CoOwnershipVehicle.Vehicle.Api;
using System.Net.Http;
using System.Net.Http.Json;
using System;

namespace CoOwnershipVehicle.Vehicle.Api.Tests
{
    public class VehicleControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public VehicleControllerTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task GetVehicles_ReturnsSuccessStatusCode()
        {
            // Arrange

            // Act
            var response = await _client.GetAsync("/api/vehicle");

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType.ToString());
        }

        [Fact]
        public async Task GetVehicle_ReturnsVehicleDetails()
        {
            // Arrange
            var vehicleId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6");

            // Act
            var response = await _client.GetAsync($"/api/vehicle/{vehicleId}");

            // Assert
            response.EnsureSuccessStatusCode();
            var vehicle = await response.Content.ReadFromJsonAsync<CoOwnershipVehicle.Domain.Entities.Vehicle>();
            Assert.NotNull(vehicle);
            Assert.Equal(vehicleId, vehicle.Id);
            Assert.Equal("VIN123456789012345", vehicle.Vin);
        }
    }
}

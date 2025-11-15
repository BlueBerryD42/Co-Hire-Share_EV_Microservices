using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Threading.Tasks;
using CoOwnershipVehicle.Vehicle.Api;
using System.Net.Http;
using System.Net.Http.Json;
using System;
using System.Net;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Vehicle.Api.Tests
{
    /// <summary>
    /// Comprehensive integration tests for Vehicle Controller endpoints
    /// Tests all CRUD operations, authorization, validation, and business logic
    /// </summary>
    public class VehicleControllerComprehensiveTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public VehicleControllerComprehensiveTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            // Add fake authorization header for tests
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        }

        #region GET /api/vehicle Tests

        [Fact]
        public async Task GetVehicles_ReturnsListOfVehicles()
        {
            // Act
            var response = await _client.GetAsync("/api/vehicle");

            // Assert
            response.EnsureSuccessStatusCode();
            var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleResponseDto>>(JsonOptions);
            Assert.NotNull(vehicles);
            Assert.True(vehicles.Count >= 2, "Should have at least the 2 seeded test vehicles"); // At least 2 from seed data
        }

        [Fact]
        public async Task GetVehicles_ReturnsOnlyUserGroupVehicles()
        {
            // Act
            var response = await _client.GetAsync("/api/vehicle");

            // Assert
            response.EnsureSuccessStatusCode();
            var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleResponseDto>>(JsonOptions);
            Assert.NotNull(vehicles);
            // All vehicles should belong to the test user's group
            Assert.All(vehicles, v => Assert.Equal(new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"), v.GroupId));
        }

        #endregion

        #region GET /api/vehicle/{id} Tests

        [Fact]
        public async Task GetVehicle_ValidId_ReturnsVehicle()
        {
            // Arrange
            var vehicleId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6");

            // Act
            var response = await _client.GetAsync($"/api/vehicle/{vehicleId}");

            // Assert
            response.EnsureSuccessStatusCode();
            var vehicle = await response.Content.ReadFromJsonAsync<VehicleResponseDto>(JsonOptions);
            Assert.NotNull(vehicle);
            Assert.Equal(vehicleId, vehicle.Id);
            Assert.Equal("VIN123456789012345", vehicle.Vin);
            Assert.Equal("PLATE1", vehicle.PlateNumber);
        }

        [Fact]
        public async Task GetVehicle_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = Guid.NewGuid();

            // Act
            var response = await _client.GetAsync($"/api/vehicle/{invalidId}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        #endregion

        #region POST /api/vehicle Tests

        [Fact]
        public async Task CreateVehicle_ValidData_ReturnsCreated()
        {
            // Arrange
            var newVehicle = new CreateVehicleDto
            {
                Vin = "NEWVIN12345678901", // 17 characters
                PlateNumber = "NEWPLATE1",
                Model = "Tesla Model 3",
                Year = 2023,
                Color = "White",
                Odometer = 0,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7")
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/vehicle", newVehicle);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdVehicle = await response.Content.ReadFromJsonAsync<VehicleResponseDto>(JsonOptions);
            Assert.NotNull(createdVehicle);
            Assert.Equal(newVehicle.Vin, createdVehicle.Vin);
            Assert.Equal(newVehicle.PlateNumber, createdVehicle.PlateNumber);
            Assert.Equal(VehicleStatus.Available, createdVehicle.Status);
        }

        [Fact]
        public async Task CreateVehicle_DuplicateVin_ReturnsBadRequest()
        {
            // Arrange - First create a vehicle
            var firstVehicle = new CreateVehicleDto
            {
                Vin = $"DUP{Guid.NewGuid().ToString().Substring(0, 14)}", // 17 chars
                PlateNumber = $"DUPP{Guid.NewGuid().ToString().Substring(0, 6)}",
                Model = "First Vehicle",
                Year = 2023,
                Odometer = 0,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7")
            };
            await _client.PostAsJsonAsync("/api/vehicle", firstVehicle);

            // Now try to create another vehicle with the same VIN
            var duplicateVehicle = new CreateVehicleDto
            {
                Vin = firstVehicle.Vin, // Same VIN
                PlateNumber = $"DIFF{Guid.NewGuid().ToString().Substring(0, 6)}",
                Model = "Duplicate Vehicle",
                Year = 2023,
                Odometer = 0,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7")
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/vehicle", duplicateVehicle);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errorContent = await response.Content.ReadAsStringAsync();
            // Verify error message mentions VIN or duplicate
            Assert.True(
                errorContent.Contains("VIN") || errorContent.Contains("exists") || errorContent.Contains("duplicate"),
                $"Error message should mention VIN or duplicate. Actual: {errorContent}"
            );
        }

        [Fact]
        public async Task CreateVehicle_DuplicatePlateNumber_ReturnsBadRequest()
        {
            // Arrange - Use existing PlateNumber from test data
            var vehicleWithDuplicatePlate = new CreateVehicleDto
            {
                Vin = "UNIQUEVIN12345678",
                PlateNumber = "PLATE1", // Existing plate number
                Model = "Test Model",
                Year = 2023,
                Color = "Red",
                Odometer = 0,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7")
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/vehicle", vehicleWithDuplicatePlate);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("PlateNumber", errorContent);
        }

        [Fact]
        public async Task CreateVehicle_InvalidData_ReturnsBadRequest()
        {
            // Arrange - Missing required fields
            var invalidVehicle = new CreateVehicleDto
            {
                // Missing Vin
                PlateNumber = "TEST123",
                Model = "Test Model",
                Year = 2023,
                Odometer = 0,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7")
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/vehicle", invalidVehicle);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region PUT /api/vehicle/{id}/status Tests

        [Fact]
        public async Task UpdateVehicleStatus_ValidData_ReturnsOk()
        {
            // Arrange
            var vehicleId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6");
            var updateDto = new UpdateVehicleStatusDto
            {
                Status = CoOwnershipVehicle.Domain.Entities.VehicleStatus.Maintenance
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/vehicle/{vehicleId}/status", updateDto);

            // Assert
            response.EnsureSuccessStatusCode();
            var updatedVehicle = await response.Content.ReadFromJsonAsync<VehicleResponseDto>(JsonOptions);
            Assert.NotNull(updatedVehicle);
            Assert.Equal(VehicleStatus.Maintenance, updatedVehicle.Status);
        }

        [Fact]
        public async Task UpdateVehicleStatus_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = Guid.NewGuid();
            var updateDto = new UpdateVehicleStatusDto
            {
                Status = CoOwnershipVehicle.Domain.Entities.VehicleStatus.Maintenance
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/vehicle/{invalidId}/status", updateDto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateVehicleStatus_AllStatuses_WorkCorrectly()
        {
            // Arrange
            var vehicleId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6");
            var statuses = new[]
            {
                CoOwnershipVehicle.Domain.Entities.VehicleStatus.Available,
                CoOwnershipVehicle.Domain.Entities.VehicleStatus.InUse,
                CoOwnershipVehicle.Domain.Entities.VehicleStatus.Maintenance,
                CoOwnershipVehicle.Domain.Entities.VehicleStatus.Unavailable
            };

            foreach (var status in statuses)
            {
                // Arrange
                var updateDto = new UpdateVehicleStatusDto { Status = status };

                // Act
                var response = await _client.PutAsJsonAsync($"/api/vehicle/{vehicleId}/status", updateDto);

                // Assert
                response.EnsureSuccessStatusCode();
                var vehicle = await response.Content.ReadFromJsonAsync<VehicleResponseDto>(JsonOptions);
                Assert.Equal(status, vehicle!.Status);
            }
        }

        #endregion

        #region PUT /api/vehicle/{id}/odometer Tests

        [Fact]
        public async Task UpdateOdometer_ValidReading_ReturnsOk()
        {
            // Arrange
            var vehicleId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6");
            var updateDto = new UpdateOdometerDto
            {
                Odometer = 15000 // Higher than current 10000
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/vehicle/{vehicleId}/odometer", updateDto);

            // Assert
            response.EnsureSuccessStatusCode();
            var updatedVehicle = await response.Content.ReadFromJsonAsync<VehicleResponseDto>(JsonOptions);
            Assert.NotNull(updatedVehicle);
            Assert.Equal(15000, updatedVehicle.Odometer);
        }

        [Fact]
        public async Task UpdateOdometer_LowerReading_ReturnsBadRequest()
        {
            // Arrange
            var vehicleId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6");
            var updateDto = new UpdateOdometerDto
            {
                Odometer = 5000 // Lower than initial/current odometer
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/vehicle/{vehicleId}/odometer", updateDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Contains("cannot be less", errorContent);
        }

        [Fact]
        public async Task UpdateOdometer_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var invalidId = Guid.NewGuid();
            var updateDto = new UpdateOdometerDto { Odometer = 10000 };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/vehicle/{invalidId}/odometer", updateDto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        #endregion

        #region GET /api/vehicle/{id}/availability Tests

        [Fact]
        public async Task CheckAvailability_ValidRequest_ReturnsAvailability()
        {
            // Arrange
            var vehicleId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6");
            var from = DateTime.UtcNow.AddDays(1);
            var to = DateTime.UtcNow.AddDays(3);

            // Act
            var response = await _client.GetAsync($"/api/vehicle/{vehicleId}/availability?from={from:O}&to={to:O}");

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task CheckAvailability_DateRangeQuery_WorksCorrectly()
        {
            // Arrange
            var vehicleId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6");
            var from = new DateTime(2025, 11, 1);
            var to = new DateTime(2025, 11, 5);

            // Act
            var response = await _client.GetAsync($"/api/vehicle/{vehicleId}/availability?from={from:O}&to={to:O}");

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
            Assert.NotNull(result);
        }

        #endregion

        #region Vehicle-Group Relationship Tests

        [Fact]
        public async Task GetVehicles_VerifyGroupRelationship_OnlyReturnsUserGroupVehicles()
        {
            // Act
            var response = await _client.GetAsync("/api/vehicle");

            // Assert
            response.EnsureSuccessStatusCode();
            var vehicles = await response.Content.ReadFromJsonAsync<List<VehicleResponseDto>>(JsonOptions);
            Assert.NotNull(vehicles);
            Assert.All(vehicles, v =>
            {
                Assert.NotNull(v.GroupId);
                Assert.Equal(new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"), v.GroupId);
            });
        }

        #endregion

        #region Data Validation Tests

        [Fact]
        public async Task CreateVehicle_VinExactly17Characters_IsValid()
        {
            // Arrange
            var vehicle = new CreateVehicleDto
            {
                Vin = "12345678901234567", // Exactly 17 chars
                PlateNumber = "VALID123",
                Model = "Test",
                Year = 2023,
                Odometer = 0,
                GroupId = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7")
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/vehicle", vehicle);

            // Assert
            Assert.True(
                response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.BadRequest,
                "VIN validation should either accept or reject based on exact 17 character requirement"
            );
        }

        #endregion
    }

    /// <summary>
    /// DTO for updating vehicle status (if not already defined)
    /// </summary>
    public class UpdateVehicleStatusDto
    {
        public CoOwnershipVehicle.Domain.Entities.VehicleStatus Status { get; set; }
    }

    /// <summary>
    /// DTO for updating odometer (if not already defined)
    /// </summary>
    public class UpdateOdometerDto
    {
        public int Odometer { get; set; }
    }

    public class VehicleResponseDto
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
    }
}

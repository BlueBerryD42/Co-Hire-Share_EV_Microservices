
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Vehicle.Api.Data;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Vehicle.Api.Services;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using CoOwnershipVehicle.Domain.Entities;
using System.Linq;

namespace CoOwnershipVehicle.Vehicle.Api.Tests
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
        where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove the app's VehicleDbContext registration.
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType ==
                        typeof(DbContextOptions<VehicleDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add VehicleDbContext using an in-memory database for testing.
                services.AddDbContext<VehicleDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });

                // Build the service provider.
                var sp = services.BuildServiceProvider();

                // Create a scope to obtain a reference to the database contexts
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<VehicleDbContext>();
                    var logger = scopedServices
                        .GetRequiredService<ILogger<CustomWebApplicationFactory<TProgram>>>();

                    // Ensure the database is created.
                    db.Database.EnsureCreated();

                    try
                    {
                        // Seed the database with test data.
                        Utilities.InitializeDbForTests(db);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred seeding the " +
                            "database with test messages. Error: {Message}", ex.Message);
                    }
                }

                // Mock IGroupServiceClient
                var mockGroupService = new Mock<IGroupServiceClient>();
                mockGroupService.Setup(x => x.GetUserGroups(It.IsAny<string>()))
                    .ReturnsAsync(new List<CoOwnershipVehicle.Vehicle.Api.DTOs.GroupServiceGroupDto>
                    {
                        new CoOwnershipVehicle.Vehicle.Api.DTOs.GroupServiceGroupDto
                        {
                            Id = new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7") // Same GroupId as test vehicles
                        }
                    });
                services.AddSingleton(mockGroupService.Object);

                // Mock IBookingServiceClient
                var mockBookingService = new Mock<IBookingServiceClient>();
                mockBookingService.Setup(x => x.CheckAvailabilityAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<string>()))
                    .ReturnsAsync(new CoOwnershipVehicle.Vehicle.Api.DTOs.BookingConflictDto
                    {
                        VehicleId = It.IsAny<Guid>(),
                        HasConflicts = false,
                        ConflictingBookings = new List<CoOwnershipVehicle.Vehicle.Api.DTOs.BookingDto>()
                    });
                services.AddSingleton(mockBookingService.Object);

                // Mock Authentication - Replace JWT with Test scheme
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
            });
        }
    }

    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "3fa85f64-5717-4562-b3fc-2c963f66afa9"), // Valid GUID
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim(ClaimTypes.Role, "SystemAdmin"), // Default role for tests
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            var result = AuthenticateResult.Success(ticket);

            return Task.FromResult(result);
        }
    }
}

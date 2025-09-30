using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.User.Api.Data;

public static class UserDataSeeder
{
    public static async Task SeedAsync(UserDbContext context)
    {
        // User service doesn't need to seed users - they come from Auth service
        // Just ensure the database is created and ready
        await context.Database.EnsureCreatedAsync();
    }
}

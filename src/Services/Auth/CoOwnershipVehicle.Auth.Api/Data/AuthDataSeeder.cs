using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Auth.Api.Data;

public static class AuthDataSeeder
{
    public static async Task SeedAsync(AuthDbContext context, UserManager<User> userManager, RoleManager<IdentityRole<Guid>> roleManager)
    {
        // Seed Roles
        await SeedRolesAsync(roleManager);
        
        // Seed Users
        await SeedUsersAsync(userManager);
        
        // Save changes after user creation
        await context.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        var roles = new[]
        {
            "SystemAdmin",
            "Staff", 
            "GroupAdmin",
            "CoOwner"
        };

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpper()
                });
            }
        }
    }

    private static async Task SeedUsersAsync(UserManager<User> userManager)
    {
        // Admin User
        var adminEmail = "admin@coev.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new User
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                EmailConfirmed = true,
                Role = UserRole.SystemAdmin,
                KycStatus = KycStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(admin, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "SystemAdmin");
            }
        }

        // Staff User
        var staffEmail = "staff@coev.com";
        if (await userManager.FindByEmailAsync(staffEmail) == null)
        {
            var staff = new User
            {
                Id = Guid.NewGuid(),
                UserName = staffEmail,
                Email = staffEmail,
                FirstName = "Staff",
                LastName = "Member",
                EmailConfirmed = true,
                Role = UserRole.Staff,
                KycStatus = KycStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(staff, "Staff123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(staff, "Staff");
            }
        }

        // Sample Co-owners
        var coOwners = new[]
        {
            new { Email = "john.doe@example.com", FirstName = "John", LastName = "Doe" },
            new { Email = "jane.smith@example.com", FirstName = "Jane", LastName = "Smith" },
            new { Email = "bob.wilson@example.com", FirstName = "Bob", LastName = "Wilson" }
        };

        foreach (var owner in coOwners)
        {
            if (await userManager.FindByEmailAsync(owner.Email) == null)
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    UserName = owner.Email,
                    Email = owner.Email,
                    FirstName = owner.FirstName,
                    LastName = owner.LastName,
                    EmailConfirmed = true,
                    Role = UserRole.CoOwner,
                    KycStatus = KycStatus.Approved,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, "User123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "CoOwner");
                }
            }
        }
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Data.Seeding;

public static class DataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, UserManager<User> userManager, RoleManager<IdentityRole<Guid>> roleManager)
    {
        // Seed Roles
        await SeedRolesAsync(roleManager);
        
        // Seed Users
        await SeedUsersAsync(userManager);
        
        // Save changes after user creation
        await context.SaveChangesAsync();
        
        // Seed Groups and other data that depends on users
        await SeedGroupsAsync(context);
        await SeedVehiclesAsync(context);
        
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

    private static async Task SeedGroupsAsync(ApplicationDbContext context)
    {
        if (!await context.OwnershipGroups.AnyAsync())
        {
            var johnUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "john.doe@example.com");
            var janeUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "jane.smith@example.com");
            var bobUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "bob.wilson@example.com");

            if (johnUser != null && janeUser != null && bobUser != null)
            {
                var group = new OwnershipGroup
                {
                    Id = Guid.NewGuid(),
                    Name = "Tesla Model 3 Group",
                    Description = "Shared ownership group for Tesla Model 3",
                    Status = GroupStatus.Active,
                    CreatedBy = johnUser.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.OwnershipGroups.Add(group);
                await context.SaveChangesAsync();

                // Add group members
                var members = new[]
                {
                    new GroupMember
                    {
                        Id = Guid.NewGuid(),
                        GroupId = group.Id,
                        UserId = johnUser.Id,
                        SharePercentage = 0.5m, // 50%
                        RoleInGroup = GroupRole.Admin,
                        JoinedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new GroupMember
                    {
                        Id = Guid.NewGuid(),
                        GroupId = group.Id,
                        UserId = janeUser.Id,
                        SharePercentage = 0.3m, // 30%
                        RoleInGroup = GroupRole.Member,
                        JoinedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new GroupMember
                    {
                        Id = Guid.NewGuid(),
                        GroupId = group.Id,
                        UserId = bobUser.Id,
                        SharePercentage = 0.2m, // 20%
                        RoleInGroup = GroupRole.Member,
                        JoinedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                context.GroupMembers.AddRange(members);
            }
        }
    }

    private static async Task SeedVehiclesAsync(ApplicationDbContext context)
    {
        if (!await context.Vehicles.AnyAsync())
        {
            var group = await context.OwnershipGroups.FirstOrDefaultAsync();
            
            if (group != null)
            {
                var vehicle = new Vehicle
                {
                    Id = Guid.NewGuid(),
                    Vin = "5YJ3E1EA3KF123456",
                    PlateNumber = "TESLA01",
                    Model = "Tesla Model 3",
                    Year = 2023,
                    Color = "White",
                    Status = VehicleStatus.Available,
                    Odometer = 5000,
                    GroupId = group.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.Vehicles.Add(vehicle);
            }
        }
    }
}

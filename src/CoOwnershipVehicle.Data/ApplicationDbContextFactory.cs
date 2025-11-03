using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoOwnershipVehicle.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Sử dụng connection string cho Group database
        var connectionString = "Server=DESKTOP-42T3FDV\\NGUYENTRAN;Database=CoOwnershipVehicle_Group;User Id=sa;Password=Naa1512004**;TrustServerCertificate=true;MultipleActiveResultSets=true";

        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);     
    }
}
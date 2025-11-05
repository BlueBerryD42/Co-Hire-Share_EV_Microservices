using System.IO;
using CoOwnershipVehicle.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CoOwnershipVehicle.Booking.Api.Data;

public class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var dbParams = EnvironmentHelper.GetDatabaseConnectionParams(configuration);
        dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_BOOKING", configuration) ?? "CoOwnershipVehicle_Booking";

        var optionsBuilder = new DbContextOptionsBuilder<BookingDbContext>();
        optionsBuilder.UseSqlServer(dbParams.GetConnectionString(), options =>
        {
            options.MigrationsAssembly("CoOwnershipVehicle.Booking.Api");
        });

        return new BookingDbContext(optionsBuilder.Options);
    }
}


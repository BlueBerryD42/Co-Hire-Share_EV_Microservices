
using Microsoft.Extensions.Configuration;

namespace CoOwnershipVehicle.Shared.Configuration
{
    public static class MockEnvironmentHelper
    {
        public static DatabaseConnectionParams GetDatabaseConnectionParams(IConfiguration configuration)
        {
            return new DatabaseConnectionParams
            {
                Server = "localhost",
                Port = "1433",
                Database = "TestDb",
                UserId = "sa",
                Password = "yourStrong(!)Password"
            };
        }

        public static string? GetEnvironmentVariable(string key, IConfiguration configuration)
        {
            return "TestValue";
        }

        public static void LogEnvironmentStatus(string serviceName, IConfiguration configuration)
        {
            // Do nothing in tests
        }

        public static void LogFinalConnectionDetails(string serviceName, string databaseName, IConfiguration configuration)
        {
            // Do nothing in tests
        }

        public static JwtConfigParams GetJwtConfigParams(IConfiguration configuration)
        {
            return new JwtConfigParams
            {
                SecretKey = "supersecretkeythatisatleast32characterslong",
                Issuer = "testissuer",
                Audience = "testaudience"
            };
        }
    }
}

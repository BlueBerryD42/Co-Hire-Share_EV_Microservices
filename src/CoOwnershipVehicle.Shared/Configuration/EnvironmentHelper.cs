using Microsoft.Extensions.Configuration;

namespace CoOwnershipVehicle.Shared.Configuration;

/// <summary>
/// Shared helper class for loading environment variables with fallback support.
/// Provides a consistent way to load configuration values from:
/// 1. System environment variables (production)
/// 2. Configuration files (appsettings.json)
/// 3. .env file (development fallback)
/// </summary>
public static class EnvironmentHelper
{
    /// <summary>
    /// Gets an environment variable with fallback support.
    /// </summary>
    /// <param name="key">The environment variable key</param>
    /// <param name="configuration">Optional configuration object for appsettings.json fallback</param>
    /// <param name="defaultValue">Optional default value if all sources fail</param>
    /// <returns>The environment variable value or null if not found</returns>
    public static string? GetEnvironmentVariable(string key, IConfiguration? configuration = null, string? defaultValue = null)
    {
        // 1. Try system environment variable (production)
        var value = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrEmpty(value)) 
            return value;
        
        // 2. Try configuration (appsettings.json)
        if (configuration != null)
        {
            value = configuration[key];
            if (!string.IsNullOrEmpty(value)) 
                return value;
        }
        
        // 3. Try .env file (development fallback)
        try
        {
            var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            if (File.Exists(envFile))
            {
                var lines = File.ReadAllLines(envFile);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith($"{key}=") && !trimmedLine.StartsWith("#"))
                    {
                        var envValue = trimmedLine.Substring(key.Length + 1).Trim();
                        // Remove quotes if present
                        if ((envValue.StartsWith('"') && envValue.EndsWith('"')) ||
                            (envValue.StartsWith('\'') && envValue.EndsWith('\'')))
                        {
                            envValue = envValue.Substring(1, envValue.Length - 2);
                        }
                        return envValue;
                    }
                }
            }
        }
        catch
        {
            // Ignore .env file errors - continue with default value
        }
        
        // 4. Return default value if provided
        return defaultValue;
    }

    /// <summary>
    /// Gets an environment variable with fallback support, throwing an exception if not found.
    /// </summary>
    /// <param name="key">The environment variable key</param>
    /// <param name="configuration">Optional configuration object for appsettings.json fallback</param>
    /// <returns>The environment variable value</returns>
    /// <exception cref="InvalidOperationException">Thrown when the environment variable is not found in any source</exception>
    public static string GetRequiredEnvironmentVariable(string key, IConfiguration? configuration = null)
    {
        var value = GetEnvironmentVariable(key, configuration);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Required environment variable '{key}' not configured. " +
                "Checked: Environment variables, appsettings.json, and .env file");
        }
        return value;
    }

    /// <summary>
    /// Gets multiple environment variables for database connection string construction.
    /// </summary>
    /// <param name="configuration">Optional configuration object</param>
    /// <returns>Database connection parameters</returns>
    public static DatabaseConnectionParams GetDatabaseConnectionParams(IConfiguration? configuration = null)
    {
        return new DatabaseConnectionParams
        {
            Server = GetEnvironmentVariable("DB_SERVER", configuration) ?? "localhost",
            Database = GetEnvironmentVariable("DB_DATABASE", configuration) ?? "",
            User = GetEnvironmentVariable("DB_USER", configuration) ?? "sa",
            Password = GetEnvironmentVariable("DB_PASSWORD", configuration) ?? "",
            TrustServerCertificate = GetEnvironmentVariable("DB_TRUST_CERT", configuration) ?? "true",
            MultipleActiveResultSets = GetEnvironmentVariable("DB_MULTIPLE_ACTIVE_RESULTS", configuration) ?? "true"
        };
    }

    /// <summary>
    /// Gets JWT configuration parameters.
    /// </summary>
    /// <param name="configuration">Optional configuration object</param>
    /// <returns>JWT configuration parameters</returns>
    public static JwtConfigParams GetJwtConfigParams(IConfiguration? configuration = null)
    {
        return new JwtConfigParams
        {
            SecretKey = GetRequiredEnvironmentVariable("JWT_SECRET_KEY", configuration),
            Issuer = GetEnvironmentVariable("JWT_ISSUER", configuration) ?? "CoOwnershipVehicle.Auth.Api",
            Audience = GetEnvironmentVariable("JWT_AUDIENCE", configuration) ?? "CoOwnershipVehicleApp",
            ExpiryMinutes = int.Parse(GetEnvironmentVariable("JWT_EXPIRY_MINUTES", configuration) ?? "60")
        };
    }

    /// <summary>
    /// Gets RabbitMQ connection string.
    /// </summary>
    /// <param name="configuration">Optional configuration object</param>
    /// <returns>RabbitMQ connection string</returns>
    public static string GetRabbitMqConnection(IConfiguration? configuration = null)
    {
        return GetEnvironmentVariable("RABBITMQ_CONNECTION", configuration) ?? "amqp://guest:guest@localhost:5672/";
    }

    /// <summary>
    /// Logs environment variable status for debugging purposes.
    /// </summary>
    /// <param name="serviceName">Name of the service for logging</param>
    /// <param name="configuration">Optional configuration object</param>
    public static void LogEnvironmentStatus(string serviceName, IConfiguration? configuration = null)
    {
        Console.WriteLine($"[DEBUG] {serviceName} Environment Check:");
        
        var dbParams = GetDatabaseConnectionParams(configuration);
        Console.WriteLine($"[DEBUG] DB_SERVER: {dbParams.Server}");
        Console.WriteLine($"[DEBUG] DB_DATABASE: {dbParams.Database}");
        Console.WriteLine($"[DEBUG] DB_USER: {dbParams.User}");
        Console.WriteLine($"[DEBUG] DB_PASSWORD: {(string.IsNullOrEmpty(dbParams.Password) ? "NOT SET" : "*****")}");
        
        var jwtSecret = GetEnvironmentVariable("JWT_SECRET_KEY", configuration);
        Console.WriteLine($"[DEBUG] JWT_SECRET_KEY: {(string.IsNullOrEmpty(jwtSecret) ? "NOT SET" : "*****")}");
        
        var rabbitMq = GetEnvironmentVariable("RABBITMQ_CONNECTION", configuration);
        Console.WriteLine($"[DEBUG] RABBITMQ_CONNECTION: {(string.IsNullOrEmpty(rabbitMq) ? "NOT SET" : "SET")}");
    }
}

/// <summary>
/// Database connection parameters
/// </summary>
public class DatabaseConnectionParams
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TrustServerCertificate { get; set; } = "true";
    public string MultipleActiveResultSets { get; set; } = "true";

    /// <summary>
    /// Gets the complete connection string
    /// </summary>
    public string GetConnectionString()
    {
        return $"Server={Server};Database={Database};User Id={User};Password={Password};" +
               $"TrustServerCertificate={TrustServerCertificate};MultipleActiveResultSets={MultipleActiveResultSets}";
    }
}

/// <summary>
/// JWT configuration parameters
/// </summary>
public class JwtConfigParams
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

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
            // Look for .env file in project root (go up from current directory)
            var currentDir = Directory.GetCurrentDirectory();
            var envFile = Path.Combine(currentDir, ".env");
            
            // If not found in current directory, try going up to find project root
            if (!File.Exists(envFile))
            {
                var dir = new DirectoryInfo(currentDir);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, ".env")))
                {
                    dir = dir.Parent;
                }
                if (dir != null)
                {
                    envFile = Path.Combine(dir.FullName, ".env");
                }
            }
            
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
    /// Gets email configuration parameters
    /// </summary>
    public static EmailConfigParams GetEmailConfigParams(IConfiguration? configuration = null)
    {
        return new EmailConfigParams
        {
            SmtpHost = GetEnvironmentVariable("SMTP_HOST", configuration),
            SmtpPort = int.Parse(GetEnvironmentVariable("SMTP_PORT", configuration) ?? "587"),
            SmtpUsername = GetEnvironmentVariable("SMTP_USERNAME", configuration),
            SmtpPassword = GetEnvironmentVariable("SMTP_PASSWORD", configuration),
            UseSsl = bool.Parse(GetEnvironmentVariable("SMTP_USE_SSL", configuration) ?? "true"),
            FromEmail = GetEnvironmentVariable("EMAIL_FROM", configuration),
            FromName = GetEnvironmentVariable("EMAIL_FROM_NAME", configuration) ?? "Co-Ownership Vehicle",
            FrontendUrl = GetEnvironmentVariable("FRONTEND_URL", configuration) ?? "https://localhost:3000"
        };
    }

    /// <summary>
    /// Gets Redis configuration parameters
    /// </summary>
    public static RedisConfigParams GetRedisConfigParams(IConfiguration? configuration = null)
    {
        return new RedisConfigParams
        {
            ConnectionString = GetEnvironmentVariable("REDIS_CONNECTION_STRING", configuration) ?? "localhost:6379",
            Database = int.Parse(GetEnvironmentVariable("REDIS_DATABASE", configuration) ?? "0"),
            KeyPrefix = GetEnvironmentVariable("REDIS_KEY_PREFIX", configuration) ?? "CoOwnershipVehicle:"
        };
    }

    /// <summary>
    /// Logs environment variable status for debugging purposes.
    /// </summary>
    /// <param name="serviceName">Name of the service for logging</param>
    /// <param name="configuration">Optional configuration object</param>
    public static void LogEnvironmentStatus(string serviceName, IConfiguration? configuration = null)
    {
        Console.WriteLine($"[DEBUG] {serviceName} Environment Check:");
        
        // Database configuration
        var dbParams = GetDatabaseConnectionParams(configuration);
        Console.WriteLine($"[DEBUG] DB_SERVER: {dbParams.Server}");
        Console.WriteLine($"[DEBUG] DB_DATABASE: {dbParams.Database}");
        Console.WriteLine($"[DEBUG] DB_USER: {dbParams.User}");
        Console.WriteLine($"[DEBUG] DB_PASSWORD: {(string.IsNullOrEmpty(dbParams.Password) ? "NOT SET" : "*****")}");
        Console.WriteLine($"[DEBUG] DB_TRUST_CERT: {dbParams.TrustServerCertificate}");
        Console.WriteLine($"[DEBUG] DB_MULTIPLE_ACTIVE_RESULTS: {dbParams.MultipleActiveResultSets}");
        Console.WriteLine($"[DEBUG] ACTUAL_CONNECTION_STRING: {dbParams.GetConnectionString()}");
        
        // Service-specific database names
        var dbAuth = GetEnvironmentVariable("DB_AUTH", configuration);
        var dbUser = GetEnvironmentVariable("DB_USER_SERVICE", configuration);
        var dbGroup = GetEnvironmentVariable("DB_GROUP", configuration);
        var dbVehicle = GetEnvironmentVariable("DB_VEHICLE", configuration);
        var dbPayment = GetEnvironmentVariable("DB_PAYMENT", configuration);
        var dbBooking = GetEnvironmentVariable("DB_BOOKING", configuration);
        var dbNotification = GetEnvironmentVariable("DB_NOTIFICATION", configuration);
        var dbAnalytics = GetEnvironmentVariable("DB_ANALYTICS", configuration);
        
        Console.WriteLine($"[DEBUG] DB_AUTH: {dbAuth ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] DB_USER_SERVICE: {dbUser ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] DB_GROUP: {dbGroup ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] DB_VEHICLE: {dbVehicle ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] DB_PAYMENT: {dbPayment ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] DB_BOOKING: {dbBooking ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] DB_NOTIFICATION: {dbNotification ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] DB_ANALYTICS: {dbAnalytics ?? "NOT SET"}");
        
        // JWT configuration
        var jwtConfig = GetJwtConfigParams(configuration);
        Console.WriteLine($"[DEBUG] JWT_SECRET_KEY: {(string.IsNullOrEmpty(jwtConfig.SecretKey) ? "NOT SET" : "*****")}");
        Console.WriteLine($"[DEBUG] JWT_ISSUER: {jwtConfig.Issuer}");
        Console.WriteLine($"[DEBUG] JWT_AUDIENCE: {jwtConfig.Audience}");
        Console.WriteLine($"[DEBUG] JWT_EXPIRY_MINUTES: {jwtConfig.ExpiryMinutes}");
        
        // RabbitMQ configuration
        var rabbitMq = GetEnvironmentVariable("RABBITMQ_CONNECTION", configuration);
        Console.WriteLine($"[DEBUG] RABBITMQ_CONNECTION: {(string.IsNullOrEmpty(rabbitMq) ? "NOT SET" : "SET")}");
        
        // Redis configuration
        var redisConfig = GetRedisConfigParams(configuration);
        Console.WriteLine($"[DEBUG] REDIS_CONNECTION_STRING: {(string.IsNullOrEmpty(redisConfig.ConnectionString) ? "NOT SET" : "SET")}");
        Console.WriteLine($"[DEBUG] REDIS_DATABASE: {redisConfig.Database}");
        Console.WriteLine($"[DEBUG] REDIS_KEY_PREFIX: {redisConfig.KeyPrefix}");
        
        // Email configuration
        var emailConfig = GetEmailConfigParams(configuration);
        Console.WriteLine($"[DEBUG] SMTP_HOST: {emailConfig.SmtpHost ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] SMTP_PORT: {emailConfig.SmtpPort}");
        Console.WriteLine($"[DEBUG] SMTP_USERNAME: {(string.IsNullOrEmpty(emailConfig.SmtpUsername) ? "NOT SET" : "SET")}");
        Console.WriteLine($"[DEBUG] SMTP_PASSWORD: {(string.IsNullOrEmpty(emailConfig.SmtpPassword) ? "NOT SET" : "SET")}");
        Console.WriteLine($"[DEBUG] SMTP_USE_SSL: {emailConfig.UseSsl}");
        Console.WriteLine($"[DEBUG] EMAIL_FROM: {emailConfig.FromEmail ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] EMAIL_FROM_NAME: {emailConfig.FromName}");
        Console.WriteLine($"[DEBUG] FRONTEND_URL: {emailConfig.FrontendUrl}");
        
        // VNPay configuration (for Payment service)
        var vnpayTmnCode = GetEnvironmentVariable("VNPAY_TMN_CODE", configuration);
        var vnpayHashSecret = GetEnvironmentVariable("VNPAY_HASH_SECRET", configuration);
        var vnpayPaymentUrl = GetEnvironmentVariable("VNPAY_PAYMENT_URL", configuration);
        var vnpayReturnUrl = GetEnvironmentVariable("VNPAY_RETURN_URL", configuration);
        
        Console.WriteLine($"[DEBUG] VNPAY_TMN_CODE: {(string.IsNullOrEmpty(vnpayTmnCode) ? "NOT SET" : "SET")}");
        Console.WriteLine($"[DEBUG] VNPAY_HASH_SECRET: {(string.IsNullOrEmpty(vnpayHashSecret) ? "NOT SET" : "SET")}");
        Console.WriteLine($"[DEBUG] VNPAY_PAYMENT_URL: {vnpayPaymentUrl ?? "NOT SET"}");
        Console.WriteLine($"[DEBUG] VNPAY_RETURN_URL: {vnpayReturnUrl ?? "NOT SET"}");
    }

    /// <summary>
    /// Logs the final database connection details that a service will use.
    /// </summary>
    /// <param name="serviceName">Name of the service for logging</param>
    /// <param name="databaseName">The specific database name the service will use</param>
    /// <param name="configuration">Optional configuration object</param>
    public static void LogFinalConnectionDetails(string serviceName, string databaseName, IConfiguration? configuration = null)
    {
        Console.WriteLine($"[DEBUG] {serviceName} Final Connection Details:");
        
        var dbParams = GetDatabaseConnectionParams(configuration);
        dbParams.Database = databaseName;
        
        Console.WriteLine($"[DEBUG] FINAL_DATABASE_NAME: {dbParams.Database}");
        Console.WriteLine($"[DEBUG] FINAL_CONNECTION_STRING: {dbParams.GetConnectionString()}");
        Console.WriteLine();
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

/// <summary>
/// Email configuration parameters
/// </summary>
public class EmailConfigParams
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Co-Ownership Vehicle";
    public string FrontendUrl { get; set; } = "https://localhost:3000";
}

/// <summary>
/// Redis configuration parameters
/// </summary>
public class RedisConfigParams
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public int Database { get; set; } = 0;
    public string KeyPrefix { get; set; } = "CoOwnershipVehicle:";
}

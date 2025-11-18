using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Admin.Api.Services;
using CoOwnershipVehicle.Admin.Api.Services.HttpClients;
using CoOwnershipVehicle.Admin.Api.Middleware;
using CoOwnershipVehicle.Shared.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Linq;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Load .env file if it exists
var envFilePath = EnvironmentHelper.FindEnvFile();
if (!string.IsNullOrEmpty(envFilePath))
{
    ((IConfigurationBuilder)builder.Configuration).Add(new EnvFileConfigurationSource(envFilePath));
    Console.WriteLine($"[INFO] Loaded configuration from .env file: {envFilePath}");
}
else
{
    Console.WriteLine("[WARN] .env file not found. Relying on system environment variables and appsettings.json.");
}

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Co-Ownership Vehicle Admin API", 
        Version = "v1",
        Description = "Administrative service for the Co-Ownership Vehicle Management System"
    });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter your token below (without 'Bearer ' prefix).",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,  
        Scheme = "bearer",            
        BearerFormat = "JWT"             
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Database Configuration
var dbParams = EnvironmentHelper.GetDatabaseConnectionParams(builder.Configuration);
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_ADMIN", builder.Configuration) ?? "CoOwnershipVehicle_Admin";
var connectionString = dbParams.GetConnectionString();

// Log environment status (for debugging - note: ACTUAL_CONNECTION_STRING may show empty database)
EnvironmentHelper.LogEnvironmentStatus("Admin Service", builder.Configuration);
// Log final connection details AFTER setting database name (this is the actual connection string used)
EnvironmentHelper.LogFinalConnectionDetails("Admin Service", dbParams.Database, builder.Configuration);

// Log the actual connection string that will be used (with masked password)
Console.WriteLine($"[INFO] Admin Service: Using database '{dbParams.Database}' on server '{dbParams.Server}'");

builder.Services.AddDbContext<AdminDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("CoOwnershipVehicle.Admin.Api")));

// JWT Authentication
var jwtConfig = EnvironmentHelper.GetJwtConfigParams(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtConfig.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add MassTransit for message bus
var rabbitmqConnection = EnvironmentHelper.GetRabbitMqConnection(builder.Configuration);
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitmqConnection);
        cfg.ConfigureEndpoints(context);
    });
});

// Add caching
builder.Services.AddMemoryCache();

// Add HttpContextAccessor for HTTP clients
builder.Services.AddHttpContextAccessor();

// Configure HTTP clients for inter-service communication
// Detect if running in Docker (DB_SERVER == "host.docker.internal")
// Try multiple ways to get DB_SERVER: direct env var, then EnvironmentHelper, then config
var dbServer = Environment.GetEnvironmentVariable("DB_SERVER") 
    ?? EnvironmentHelper.GetEnvironmentVariable("DB_SERVER", builder.Configuration)
    ?? builder.Configuration["DB_SERVER"];
var isDocker = dbServer == "host.docker.internal";
Console.WriteLine($"[DIAGNOSTIC] DB_SERVER value: {dbServer ?? "NULL"}");
Console.WriteLine($"[DIAGNOSTIC] Running in Docker: {isDocker}");

var serviceUrls = builder.Configuration.GetSection("ServiceUrls");

// Use Docker service names when running in containers, otherwise use config/env/localhost
// When running in Docker, override any config values with Docker service names
var userServiceUrl = isDocker 
    ? "http://user-api:8080"
    : (serviceUrls["User"] ?? EnvironmentHelper.GetEnvironmentVariable("USER_SERVICE_URL", builder.Configuration) ?? "https://localhost:61602");

var groupServiceUrl = isDocker
    ? "http://group-api:8080"
    : (serviceUrls["Group"] ?? EnvironmentHelper.GetEnvironmentVariable("GROUP_SERVICE_URL", builder.Configuration) ?? "https://localhost:61603");

var vehicleServiceUrl = isDocker
    ? "http://vehicle-api:8080"
    : (serviceUrls["Vehicle"] ?? EnvironmentHelper.GetEnvironmentVariable("VEHICLE_SERVICE_URL", builder.Configuration) ?? "https://localhost:61604");

var bookingServiceUrl = isDocker
    ? "http://booking-api:8080"
    : (serviceUrls["Booking"] ?? EnvironmentHelper.GetEnvironmentVariable("BOOKING_SERVICE_URL", builder.Configuration) ?? "https://localhost:61606");

var paymentServiceUrl = isDocker
    ? "http://payment-api:8080"
    : (serviceUrls["Payment"] ?? EnvironmentHelper.GetEnvironmentVariable("PAYMENT_SERVICE_URL", builder.Configuration) ?? "https://localhost:61605");

// Log service URLs for debugging
Console.WriteLine($"[DIAGNOSTIC] Running in Docker: {isDocker}");
Console.WriteLine($"[DIAGNOSTIC] User Service URL: {userServiceUrl}");
Console.WriteLine($"[DIAGNOSTIC] Group Service URL: {groupServiceUrl}");
Console.WriteLine($"[DIAGNOSTIC] Vehicle Service URL: {vehicleServiceUrl}");
Console.WriteLine($"[DIAGNOSTIC] Booking Service URL: {bookingServiceUrl}");
Console.WriteLine($"[DIAGNOSTIC] Payment Service URL: {paymentServiceUrl}");

// Register HTTP clients
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    client.BaseAddress = new Uri(userServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IGroupServiceClient, GroupServiceClient>(client =>
{
    client.BaseAddress = new Uri(groupServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IVehicleServiceClient, VehicleServiceClient>(client =>
{
    client.BaseAddress = new Uri(vehicleServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IBookingServiceClient, BookingServiceClient>(client =>
{
    client.BaseAddress = new Uri(bookingServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IPaymentServiceClient, PaymentServiceClient>(client =>
{
    client.BaseAddress = new Uri(paymentServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// HttpClient for health checks
builder.Services.AddHttpClient();

// Services
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>();
builder.Services.AddScoped<ISystemMetricsService, SystemMetricsService>();
builder.Services.AddScoped<ISystemLogsService, SystemLogsService>();
builder.Services.AddScoped<IAlertService, AlertService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Co-Ownership Vehicle Admin API");
        c.RoutePrefix = string.Empty; // Makes Swagger available at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Performance tracking middleware
app.UsePerformanceTracking();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply pending migrations at startup
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Admin Service: Checking for pending migrations...");
        
        // Check if database can connect
        var canConnect = await context.Database.CanConnectAsync();
        if (!canConnect)
        {
            logger.LogWarning("Admin Service: Cannot connect to database. It may not exist yet. MigrateAsync will attempt to create it.");
        }
        
        // Check for pending migrations
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Admin Service: Applying {Count} pending migrations: {Migrations}", 
                pendingMigrations.Count(), string.Join(", ", pendingMigrations));
        }
        else
        {
            logger.LogInformation("Admin Service: No pending migrations. Database is up to date.");
        }
        
        // Apply migrations (this will create the database if it doesn't exist)
        await context.Database.MigrateAsync();
        logger.LogInformation("Admin Service: Migrations applied successfully.");
        
        // Verify database connection after migration
        canConnect = await context.Database.CanConnectAsync();
        if (canConnect)
        {
            logger.LogInformation("Admin Service: Database connection verified after migration.");
            
            // Try to query a table to verify migration succeeded
            try
            {
                var auditLogCount = await context.AuditLogs.CountAsync();
                logger.LogInformation("Admin Service: AuditLogs table verified. Current record count: {Count}", auditLogCount);
            }
            catch (Exception tableEx)
            {
                logger.LogError(tableEx, "Admin Service: ERROR: AuditLogs table verification failed. This may indicate a migration issue.");
            }
        }
        else
        {
            logger.LogError("Admin Service: ERROR: Cannot connect to database after migration!");
        }
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Admin Service: Failed to apply database migrations: {Message}", ex.Message);
    logger.LogError(ex, "Admin Service: Stack trace: {StackTrace}", ex.StackTrace);
    // Don't crash - let the app start and handle migrations later if needed
}

app.Run();

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

EnvironmentHelper.LogEnvironmentStatus("Admin Service", builder.Configuration);
EnvironmentHelper.LogFinalConnectionDetails("Admin Service", dbParams.Database, builder.Configuration);

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
var serviceUrls = builder.Configuration.GetSection("ServiceUrls");
var userServiceUrl = serviceUrls["User"] ?? EnvironmentHelper.GetEnvironmentVariable("USER_SERVICE_URL", builder.Configuration) ?? "https://localhost:61602";
var groupServiceUrl = serviceUrls["Group"] ?? EnvironmentHelper.GetEnvironmentVariable("GROUP_SERVICE_URL", builder.Configuration) ?? "https://localhost:61603";
var vehicleServiceUrl = serviceUrls["Vehicle"] ?? EnvironmentHelper.GetEnvironmentVariable("VEHICLE_SERVICE_URL", builder.Configuration) ?? "https://localhost:61604";
var bookingServiceUrl = serviceUrls["Booking"] ?? EnvironmentHelper.GetEnvironmentVariable("BOOKING_SERVICE_URL", builder.Configuration) ?? "https://localhost:61606";
var paymentServiceUrl = serviceUrls["Payment"] ?? EnvironmentHelper.GetEnvironmentVariable("PAYMENT_SERVICE_URL", builder.Configuration) ?? "https://localhost:61605";

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
        await context.Database.MigrateAsync();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] Failed to apply database migrations: {ex.Message}");
    Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
    // Don't crash - let the app start and handle migrations later if needed
}

app.Run();

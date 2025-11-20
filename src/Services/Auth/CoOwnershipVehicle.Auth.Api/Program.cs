using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CoOwnershipVehicle.Auth.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Auth.Api.Services;
using CoOwnershipVehicle.Shared.Configuration;
using MassTransit;
using System.Linq;

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
var dbParams = EnvironmentHelper.GetDatabaseConnectionParams(builder.Configuration);
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_AUTH", builder.Configuration) ?? "CoOwnershipVehicle_Auth";
var connectionString = dbParams.GetConnectionString();

EnvironmentHelper.LogEnvironmentStatus("Auth Service", builder.Configuration);
EnvironmentHelper.LogFinalConnectionDetails("Auth Service", dbParams.Database, builder.Configuration);

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(connectionString,
        b => b.MigrationsAssembly("CoOwnershipVehicle.Auth.Api")));

// Add Identity services
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();

// Add JWT Authentication
var jwtConfig = EnvironmentHelper.GetJwtConfigParams(builder.Configuration);

// Configure JWT settings in configuration for the JwtTokenService
builder.Configuration["JwtSettings:SecretKey"] = jwtConfig.SecretKey;
builder.Configuration["JwtSettings:Issuer"] = jwtConfig.Issuer;
builder.Configuration["JwtSettings:Audience"] = jwtConfig.Audience;
builder.Configuration["JwtSettings:ExpiryMinutes"] = jwtConfig.ExpiryMinutes.ToString();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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

// Add MassTransit for message bus
builder.Services.AddMassTransit(x =>
{
    // Add consumers
    x.AddConsumer<CoOwnershipVehicle.Auth.Api.Consumers.UserProfileUpdatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(EnvironmentHelper.GetRabbitMqConnection(builder.Configuration));
        cfg.ConfigureEndpoints(context);
    });
});

// Add Redis
// Add Redis
var redisConfig = EnvironmentHelper.GetRedisConfigParams(builder.Configuration);

StackExchange.Redis.IConnectionMultiplexer? redisConnection = null;
try
{
    var programLogger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    programLogger.LogInformation("Redis Connection String: {ConnectionString}", redisConfig.ConnectionString );
    programLogger.LogInformation("Redis Database: {Database}", redisConfig.Database);

    var configuration = new StackExchange.Redis.ConfigurationOptions();

    // Handle redis:// format
    if (redisConfig.ConnectionString.StartsWith("redis://"))
    {
        var uri = new Uri(redisConfig.ConnectionString);
        configuration.EndPoints.Add(uri.Host, uri.Port);
        if (uri.UserInfo.Contains(':'))
        {
            configuration.Password = uri.UserInfo.Split(':')[1];
        }
        programLogger.LogInformation("Redis Host: {Host}, Port: {Port}, Has Password: {HasPassword}",
            uri.Host, uri.Port, !string.IsNullOrEmpty(configuration.Password));
    }
    else
    {
        configuration = StackExchange.Redis.ConfigurationOptions.Parse(redisConfig.ConnectionString);
        programLogger.LogInformation("Redis parsed from simple format");
    }

    // Combine reliability settings from both branches
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 5;
    configuration.ConnectTimeout = 30000;
    configuration.SyncTimeout = 10000;
    configuration.AsyncTimeout = 10000;
    configuration.ResponseTimeout = 10000;
    configuration.KeepAlive = 60;
    configuration.ClientName = "CoOwnershipVehicle-Auth";

    programLogger.LogInformation("Attempting to connect to Redis...");

    // Use ConnectAsync with timeout to prevent hanging
    var connectTask = StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(configuration);
    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
    if (completedTask == timeoutTask)
    {
        programLogger.LogWarning("Redis connection timed out after 10 seconds. Continuing without Redis.");
        throw new TimeoutException("Redis connection timed out after 10 seconds");
    }

    redisConnection = await connectTask;

    // Quick ping test with timeout
    var db = redisConnection.GetDatabase(redisConfig.Database);
    var pingTask = db.PingAsync();
    var pingTimeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
    var pingCompleted = await Task.WhenAny(pingTask, pingTimeoutTask);

    if (pingCompleted == pingTimeoutTask)
    {
        programLogger.LogWarning("Redis ping timed out after 5 seconds. Continuing without Redis.");
        throw new TimeoutException("Redis ping timed out after 5 seconds");
    }

    await pingTask;

    programLogger.LogInformation(" Successfully connected to Redis");
}
catch (Exception ex)
{
    var programLogger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    programLogger.LogError(ex, "Failed to connect to Redis. Falling back to in-memory mode.");
    redisConnection = null;
}

// Register Redis DI services safely
if (redisConnection != null)
{
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(redisConnection);
    builder.Services.AddScoped<StackExchange.Redis.IDatabase>(sp =>
    {
        var redis = sp.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
        return redis.GetDatabase(redisConfig.Database);
    });
}
else
{
    // Allow app to continue gracefully without Redis
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer?>(sp => null);
    builder.Services.AddScoped<StackExchange.Redis.IDatabase?>(sp => null);
}

// Add HTTP Client for User Service (for fetching user profile data during JWT generation)
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    // Always use Docker service name when running in containers (like Group service does)
    // This ensures inter-service communication works correctly
    var userServiceUrl = "http://user-api:8080";
    
    client.BaseAddress = new Uri(userServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(5); // Short timeout for internal calls
    
    // Log using logger (Console.WriteLine may not appear in Docker logs)
    var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    logger.LogWarning("[DIAGNOSTIC] User Service BaseAddress configured: {UserServiceUrl}", userServiceUrl);
});

// Add application services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Co-Ownership Vehicle Auth API",
        Version = "v1",
        Description = "Authentication and authorization service for the Co-Ownership Vehicle Management System"
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
            Array.Empty<string>()
        }
    });
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Auth API V1");
        c.RoutePrefix = string.Empty; // Makes Swagger available at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply migrations and seed data
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await context.Database.MigrateAsync();

        // Seed initial data
        await CoOwnershipVehicle.Auth.Api.Data.AuthDataSeeder.SeedAsync(context, userManager, roleManager);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] Failed to apply database migrations or seed data: {ex.Message}");
    Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
    // Don't crash - let the app start and handle migrations later if needed
}

app.Run();

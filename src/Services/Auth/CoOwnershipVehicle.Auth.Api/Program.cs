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
var redisConfig = EnvironmentHelper.GetRedisConfigParams(builder.Configuration);

// Try to create Redis connection
StackExchange.Redis.IConnectionMultiplexer? redisConnection = null;
try
{
    var programLogger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    programLogger.LogInformation("Redis Connection String: {ConnectionString}", redisConfig.ConnectionString);
    programLogger.LogInformation("Redis Database: {Database}", redisConfig.Database);
    
    // Parse the connection string and create configuration manually
    var configuration = new StackExchange.Redis.ConfigurationOptions();
    
    // Handle redis:// format
    if (redisConfig.ConnectionString.StartsWith("redis://"))
    {
        var uri = new Uri(redisConfig.ConnectionString);
        configuration.EndPoints.Add(uri.Host, uri.Port);
        if (uri.UserInfo.Contains(':'))
        {
            configuration.Password = uri.UserInfo.Split(':')[1]; // Get password after colon
        }
        programLogger.LogInformation("Redis Host: {Host}, Port: {Port}, Has Password: {HasPassword}", 
            uri.Host, uri.Port, !string.IsNullOrEmpty(configuration.Password));
    }
    else
    {
        // Handle host:port format
        configuration = StackExchange.Redis.ConfigurationOptions.Parse(redisConfig.ConnectionString);
        programLogger.LogInformation("Redis parsed from simple format");
    }
    
    // Add retry and connection settings for better reliability
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 5; // Increased retry count
    configuration.ConnectTimeout = 30000; // Increased timeout to 30 seconds
    configuration.SyncTimeout = 10000; // Increased sync timeout to 10 seconds
    configuration.AsyncTimeout = 10000; // Added async timeout
    configuration.ResponseTimeout = 10000; // Added response timeout
    configuration.KeepAlive = 60; // Keep connection alive
    configuration.ClientName = "CoOwnershipVehicle-Auth"; // Set client name for identification
    
    programLogger.LogInformation("Attempting to connect to Redis with enhanced settings...");
    
    redisConnection = StackExchange.Redis.ConnectionMultiplexer.Connect(configuration);
    
    // Test the connection
    var database = redisConnection.GetDatabase(redisConfig.Database);
    database.Ping(); // Test connection
    
    programLogger.LogInformation("Successfully connected to Redis");
}
catch (Exception ex)
{
    var programLogger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
    programLogger.LogError(ex, "Failed to connect to Redis. Using in-memory fallback for refresh tokens.");
    programLogger.LogWarning("Redis connection failed. The application will continue with in-memory token storage.");
    redisConnection = null;
}

// Register Redis services only if connection was successful
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
    // Register null services to avoid DI issues
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer?>(sp => null);
    builder.Services.AddScoped<StackExchange.Redis.IDatabase?>(sp => null);
}

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

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    
    // Ensure database is created
    await context.Database.EnsureCreatedAsync();
    
    // Seed initial data
    await CoOwnershipVehicle.Auth.Api.Data.AuthDataSeeder.SeedAsync(context, userManager, roleManager);
}

app.Run();

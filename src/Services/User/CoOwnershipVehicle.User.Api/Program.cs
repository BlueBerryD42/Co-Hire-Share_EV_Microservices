using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CoOwnershipVehicle.User.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.User.Api.Services;
using CoOwnershipVehicle.User.Api.Consumers;
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
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_USER_SERVICE", builder.Configuration) ?? "CoOwnershipVehicle_User";
var connectionString = dbParams.GetConnectionString();

EnvironmentHelper.LogEnvironmentStatus("User Service", builder.Configuration);
EnvironmentHelper.LogFinalConnectionDetails("User Service", dbParams.Database, builder.Configuration);

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(connectionString,
        b => b.MigrationsAssembly("CoOwnershipVehicle.User.Api")));

// NOTE: User service should NOT handle authentication
// Identity services are removed - authentication is handled by Auth service only
// The UserDbContext still inherits from IdentityDbContext for the User entity structure,
// but we don't register Identity services since we don't handle passwords/tokens

// Add JWT Authentication
var jwtConfig = EnvironmentHelper.GetJwtConfigParams(builder.Configuration);

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
    x.AddConsumer<UserRegisteredConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(EnvironmentHelper.GetRabbitMqConnection(builder.Configuration));
        
        // Configure endpoints - MassTransit automatically creates queues/exchanges for consumers
        // When UserRegisteredEvent is published, it will be routed to UserRegisteredConsumer
        cfg.ConfigureEndpoints(context);
    });
});

// Add HttpClient for UserSyncService (HTTP pattern, consistent with other services)
builder.Services.AddHttpClient<UserSyncService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Add HttpClient for other service-to-service communication
builder.Services.AddHttpClient();

// Add application services
builder.Services.AddScoped<IUserService, CoOwnershipVehicle.User.Api.Services.UserService>();
builder.Services.AddScoped<IUserSyncService, CoOwnershipVehicle.User.Api.Services.UserSyncService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Co-Ownership Vehicle User API", 
        Version = "v1",
        Description = "User profile and KYC management service for the Co-Ownership Vehicle Management System"
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "User API V1");
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
        var context = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Checking for pending migrations...");
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Applying {Count} pending migrations: {Migrations}", 
                pendingMigrations.Count(), string.Join(", ", pendingMigrations));
        }
        else
        {
            logger.LogInformation("No pending migrations. Database is up to date.");
        }
        
        await context.Database.MigrateAsync();
        logger.LogInformation("Migrations applied successfully.");
        
        // Verify UserProfiles table exists
        var canConnect = await context.Database.CanConnectAsync();
        if (canConnect)
        {
            logger.LogInformation("Database connection verified.");
            // Try to query UserProfiles table to verify it exists
            try
            {
                var count = await context.UserProfiles.CountAsync();
                logger.LogInformation("UserProfiles table verified. Current record count: {Count}", count);
            }
            catch (Exception tableEx)
            {
                logger.LogError(tableEx, "ERROR: UserProfiles table verification failed. This may indicate a migration issue.");
            }
        }
        
        // Seed initial data (User service doesn't need UserManager/RoleManager)
        await CoOwnershipVehicle.User.Api.Data.UserDataSeeder.SeedAsync(context);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to apply database migrations or seed data");
    // Don't crash - let the app start and handle migrations later if needed
}

app.Run();

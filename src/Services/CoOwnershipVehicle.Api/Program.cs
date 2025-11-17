using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Data.Seeding;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Configuration;

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
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_MAIN", builder.Configuration) ?? "CoOwnershipVehicle_Main";

// Try to get connection string from configuration, but handle empty/null values properly
string? connectionString = null;
try
{
    var configConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(configConnectionString))
    {
        connectionString = configConnectionString;
    }
}
catch
{
    // If GetConnectionString fails, we'll use dbParams below
}

// Fall back to environment-based connection string
connectionString ??= EnvironmentHelper.GetEnvironmentVariable("DB_CONNECTION_STRING", builder.Configuration) ?? dbParams.GetConnectionString();

EnvironmentHelper.LogEnvironmentStatus("Main API Service", builder.Configuration);
EnvironmentHelper.LogFinalConnectionDetails("Main API Service", dbParams.Database, builder.Configuration);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString,
        b => b.MigrationsAssembly("CoOwnershipVehicle.Api")));

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
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Co-Ownership Vehicle Main API", 
        Version = "v1",
        Description = "Main API service for the Co-Ownership Vehicle Management System"
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Co-Ownership Vehicle Main API");
        c.RoutePrefix = string.Empty; // Makes Swagger available at root
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    
    await context.Database.MigrateAsync();
    
    // Seed initial data
    await DataSeeder.SeedAsync(context, userManager, roleManager);
}

app.Run();

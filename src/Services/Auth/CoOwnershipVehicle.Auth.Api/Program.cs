using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Auth.Api.Services;
using MassTransit;
using System.Linq;

// Helper function to load .env file as fallback
static string GetEnvironmentVariable(string key, IConfiguration configuration = null)
{
    // 1. Try system environment variable (production)
    var value = Environment.GetEnvironmentVariable(key);
    if (!string.IsNullOrEmpty(value)) return value;
    
    // 2. Try configuration (appsettings.json)
    if (configuration != null)
    {
        value = configuration[key];
        if (!string.IsNullOrEmpty(value)) return value;
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
                if (line.StartsWith($"{key}=") && !line.StartsWith("#"))
                {
                    return line.Substring(key.Length + 1).Trim();
                }
            }
        }
    }
    catch
    {
        // Ignore .env file errors
    }
    
    return null;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var dbServer = GetEnvironmentVariable("DB_SERVER", builder.Configuration);
var dbAuth = GetEnvironmentVariable("DB_AUTH", builder.Configuration);
var dbUser = GetEnvironmentVariable("DB_USER", builder.Configuration);
var dbPassword = GetEnvironmentVariable("DB_PASSWORD", builder.Configuration);
var dbTrustCert = GetEnvironmentVariable("DB_TRUST_CERT", builder.Configuration) ?? "true";
var dbMultipleResults = GetEnvironmentVariable("DB_MULTIPLE_ACTIVE_RESULTS", builder.Configuration) ?? "true";

Console.WriteLine($"[DEBUG] Auth Service Environment Check:");
Console.WriteLine($"[DEBUG] DB_SERVER: {dbServer}");
Console.WriteLine($"[DEBUG] DB_AUTH: {dbAuth}");
Console.WriteLine($"[DEBUG] DB_USER: {dbUser}");
Console.WriteLine($"[DEBUG] DB_PASSWORD: {(string.IsNullOrEmpty(dbPassword) ? "NOT SET" : "*****")}");

var connectionString = $"Server={dbServer};Database={dbAuth};User Id={dbUser};Password={dbPassword};TrustServerCertificate={dbTrustCert};MultipleActiveResultSets={dbMultipleResults}";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
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
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Add JWT Authentication
var secretKey = GetEnvironmentVariable("JWT_SECRET_KEY", builder.Configuration)
    ?? throw new InvalidOperationException($"JWT SecretKey not configured. Checked: Environment variables, appsettings.json, and .env file");
var issuer = GetEnvironmentVariable("JWT_ISSUER", builder.Configuration) ?? "CoOwnershipVehicle.Auth.Api";
var audience = GetEnvironmentVariable("JWT_AUDIENCE", builder.Configuration) ?? "CoOwnershipVehicleApp";

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add MassTransit for message bus
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(GetEnvironmentVariable("RABBITMQ_CONNECTION", builder.Configuration) ?? "amqp://guest:guest@localhost:5672/");
        cfg.ConfigureEndpoints(context);
    });
});

// Add application services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

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
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    
    // Ensure database is created
    await context.Database.EnsureCreatedAsync();
    
    // Seed initial data
    await CoOwnershipVehicle.Data.Seeding.DataSeeder.SeedAsync(context, userManager, roleManager);
}

app.Run();

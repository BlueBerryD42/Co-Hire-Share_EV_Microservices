using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CoOwnershipVehicle.Analytics.Api.Data;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Shared.Configuration;
using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Analytics.Api.Consumers;
using CoOwnershipVehicle.Data;

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
        Title = "Co-Ownership Vehicle Analytics API", 
        Version = "v1",
        Description = "Analytics and reporting service for the Co-Ownership Vehicle Management System"
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
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_ANALYTICS", builder.Configuration) ?? "CoOwnershipVehicle_Analytics";
var connectionString = EnvironmentHelper.GetEnvironmentVariable("DB_CONNECTION_STRING", builder.Configuration) ?? dbParams.GetConnectionString();

EnvironmentHelper.LogEnvironmentStatus("Analytics Service", builder.Configuration);
EnvironmentHelper.LogFinalConnectionDetails("Analytics Service", dbParams.Database, builder.Configuration);

builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("CoOwnershipVehicle.Analytics.Api")));

// Add ApplicationDbContext for accessing shared entities (bookings, expenses, etc.)
// Note: In a proper microservices architecture, this would be replaced with HTTP calls or event-based communication
var mainDbConnectionString = EnvironmentHelper.GetEnvironmentVariable("DB_CONNECTION_STRING", builder.Configuration) 
    ?? EnvironmentHelper.GetDatabaseConnectionParams(builder.Configuration).GetConnectionString();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(mainDbConnectionString));

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

// MassTransit for RabbitMQ
var rabbitmqConnection = EnvironmentHelper.GetRabbitMqConnection(builder.Configuration);
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitmqConnection);
        cfg.ConfigureEndpoints(context);
    });
    
    // Add consumers for analytics data collection
    x.AddConsumer<BookingAnalyticsEventConsumer>();
    x.AddConsumer<PaymentAnalyticsEventConsumer>();
    x.AddConsumer<ExpenseAnalyticsEventConsumer>();
    x.AddConsumer<VehicleAnalyticsEventConsumer>();
});

// Services
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<CoOwnershipVehicle.Analytics.Api.Services.IAIService, CoOwnershipVehicle.Analytics.Api.Services.AIService>();
builder.Services.AddHostedService<AnalyticsProcessorService>();

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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Co-Ownership Vehicle Analytics API");
        c.RoutePrefix = string.Empty; // Makes Swagger available at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply pending migrations
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
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

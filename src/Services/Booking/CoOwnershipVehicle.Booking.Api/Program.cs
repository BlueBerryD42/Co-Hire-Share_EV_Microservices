using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CoOwnershipVehicle.Booking.Api.Configuration;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.Services;
using CoOwnershipVehicle.Booking.Api.Storage;
using CoOwnershipVehicle.Shared.Configuration;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);


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
// --- Kết thúc tích hợp .env ---

// Add services to the container.
var dbParams = EnvironmentHelper.GetDatabaseConnectionParams(builder.Configuration);
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_BOOKING", builder.Configuration) ?? "CoOwnershipVehicle_Booking";
var connectionString = dbParams.GetConnectionString();

EnvironmentHelper.LogEnvironmentStatus("Booking Service", builder.Configuration);
EnvironmentHelper.LogFinalConnectionDetails("Booking Service", dbParams.Database, builder.Configuration);

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlServer(connectionString,
        b => b.MigrationsAssembly("CoOwnershipVehicle.Booking.Api")));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<ICheckInRepository, CheckInRepository>();
builder.Services.AddScoped<IDamageReportRepository, DamageReportRepository>();
builder.Services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
builder.Services.AddScoped<ILateReturnFeeRepository, LateReturnFeeRepository>();
builder.Services.AddScoped<IRecurringBookingRepository, RecurringBookingRepository>();
builder.Services.AddScoped<IBookingTemplateRepository, BookingTemplateRepository>(); // Registered BookingTemplateRepository

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
    // Register consumers
    x.AddConsumer<CoOwnershipVehicle.Booking.Api.Consumers.MaintenanceScheduledConsumer>();
    x.AddConsumer<CoOwnershipVehicle.Booking.Api.Consumers.MaintenanceCancelledConsumer>();
    x.AddConsumer<CoOwnershipVehicle.Booking.Api.Consumers.MaintenanceCompletedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(EnvironmentHelper.GetRabbitMqConnection(builder.Configuration));
        cfg.ConfigureEndpoints(context);
    });
});

// Add application services
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<ICheckInReportGenerator, CheckInReportGenerator>();
builder.Services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
builder.Services.AddScoped<ILateReturnFeeService, LateReturnFeeService>();
builder.Services.AddScoped<IDamageReportService, DamageReportService>();
builder.Services.AddScoped<IRecurringBookingService, RecurringBookingService>();
builder.Services.AddScoped<IBookingTemplateService, BookingTemplateService>(); // Registered BookingTemplateService
builder.Services.AddScoped<IQrCodeService, VehicleQrService>();
builder.Services.AddMemoryCache();
builder.Services.AddHostedService<BookingReminderBackgroundService>();
builder.Services.AddHostedService<RecurringBookingGenerationService>();
builder.Services.Configure<QrCodeOptions>(builder.Configuration.GetSection(QrCodeOptions.SectionName));
builder.Services.Configure<LateReturnFeeOptions>(builder.Configuration.GetSection(LateReturnFeeOptions.SectionName));

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
#pragma warning disable CA1416
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
#pragma warning restore CA1416
builder.Services.AddSingleton<IVirusScanner, NoOpVirusScanner>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Co-Ownership Vehicle Booking API",
        Version = "v1",
        Description = "Intelligent booking system with priority algorithms and conflict resolution for the Co-Ownership Vehicle Management System"
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Booking API V1");
        c.RoutePrefix = string.Empty; // Makes Swagger available at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply pending migrations (before starting the server)
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
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

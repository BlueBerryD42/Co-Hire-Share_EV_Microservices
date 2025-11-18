using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services.Implementations;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MassTransit; // Added for MassTransit

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

// Configure logging to exclude EventLog (prevents disposal issues on Windows)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}

// Map environment variables to configuration keys for services that need them
builder.Configuration["FileStorage:StorageType"] = EnvironmentHelper.GetEnvironmentVariable("STORAGE_TYPE", builder.Configuration, "Local");
builder.Configuration["FileStorage:LocalStoragePath"] = EnvironmentHelper.GetEnvironmentVariable("LOCAL_STORAGE_PATH", builder.Configuration)
    ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
builder.Configuration["JwtSettings:SecretKey"] = EnvironmentHelper.GetEnvironmentVariable("JWT_SECRET_KEY", builder.Configuration);
builder.Configuration["VirusScan:Enabled"] = EnvironmentHelper.GetEnvironmentVariable("VIRUS_SCAN_ENABLED", builder.Configuration, "true");
builder.Configuration["VirusScan:ScanEngine"] = EnvironmentHelper.GetEnvironmentVariable("VIRUS_SCAN_ENGINE", builder.Configuration, "Mock");
builder.Configuration["VirusScan:ClamAVHost"] = EnvironmentHelper.GetEnvironmentVariable("CLAMAV_HOST", builder.Configuration, "localhost");
builder.Configuration["VirusScan:ClamAVPort"] = EnvironmentHelper.GetEnvironmentVariable("CLAMAV_PORT", builder.Configuration, "3310");
builder.Configuration["VirusScan:TimeoutSeconds"] = EnvironmentHelper.GetEnvironmentVariable("VIRUS_SCAN_TIMEOUT_SECONDS", builder.Configuration, "30");
builder.Configuration["EmailSettings:SmtpHost"] = EnvironmentHelper.GetEnvironmentVariable("SMTP_HOST", builder.Configuration);
builder.Configuration["EmailSettings:SmtpPort"] = EnvironmentHelper.GetEnvironmentVariable("SMTP_PORT", builder.Configuration, "587");
builder.Configuration["EmailSettings:Username"] = EnvironmentHelper.GetEnvironmentVariable("SMTP_USERNAME", builder.Configuration);
builder.Configuration["EmailSettings:Password"] = EnvironmentHelper.GetEnvironmentVariable("SMTP_PASSWORD", builder.Configuration);
builder.Configuration["EmailSettings:FromEmail"] = EnvironmentHelper.GetEnvironmentVariable("EMAIL_FROM", builder.Configuration);
builder.Configuration["EmailSettings:FromName"] = EnvironmentHelper.GetEnvironmentVariable("EMAIL_FROM_NAME", builder.Configuration);
builder.Configuration["EmailSettings:EnableSsl"] = EnvironmentHelper.GetEnvironmentVariable("SMTP_USE_SSL", builder.Configuration, "true");
builder.Configuration["SignatureReminders:IntervalHours"] = EnvironmentHelper.GetEnvironmentVariable("SIGNATURE_REMINDER_INTERVAL_HOURS", builder.Configuration, "24");
builder.Configuration["SignatureReminders:BaseUrl"] = EnvironmentHelper.GetEnvironmentVariable("SIGNATURE_REMINDER_BASE_URL", builder.Configuration);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Co-Ownership Vehicle Group API",
        Version = "v1",
        Description = "Group management service for the Co-Ownership Vehicle Management System"
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

builder.Services.Configure<FileStorageOptions>(options =>
{
    options.StorageType = Enum.Parse<StorageType>(
        builder.Configuration["FileStorage:StorageType"] ?? "Local");
    options.LocalStoragePath = builder.Configuration["FileStorage:LocalStoragePath"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    options.AzureConnectionString = builder.Configuration["FileStorage:AzureConnectionString"];
    options.AzureContainerName = builder.Configuration["FileStorage:AzureContainerName"];
    options.AwsAccessKey = builder.Configuration["FileStorage:AwsAccessKey"];
    options.AwsSecretKey = builder.Configuration["FileStorage:AwsSecretKey"];
    options.AwsBucketName = builder.Configuration["FileStorage:AwsBucketName"];
    options.AwsRegion = builder.Configuration["FileStorage:AwsRegion"];
});

// Configure Virus Scanning
builder.Services.Configure<VirusScanOptions>(options =>
{
    options.Enabled = bool.Parse(builder.Configuration["VirusScan:Enabled"] ?? "true");
    options.ScanEngine = Enum.Parse<ScanEngine>(
        builder.Configuration["VirusScan:ScanEngine"] ?? "Mock");
    options.ClamAVHost = builder.Configuration["VirusScan:ClamAVHost"] ?? "localhost";
    options.ClamAVPort = int.Parse(builder.Configuration["VirusScan:ClamAVPort"] ?? "3310");
    options.TimeoutSeconds = int.Parse(builder.Configuration["VirusScan:TimeoutSeconds"] ?? "30");
});

// Register Document Services
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IVirusScanService, VirusScanService>();
builder.Services.AddScoped<ISigningTokenService, SigningTokenService>();
builder.Services.AddScoped<ICertificateGenerationService, CertificateGenerationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Register New Document Management Services
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IDocumentSearchService, DocumentSearchService>();
builder.Services.AddScoped<IDocumentShareService, DocumentShareService>();

// Register Proposal Service
builder.Services.AddScoped<CoOwnershipVehicle.Group.Api.Contracts.IProposalService, CoOwnershipVehicle.Group.Api.Services.ProposalService>();
builder.Services.AddScoped<CoOwnershipVehicle.Group.Api.Contracts.IVotingService, CoOwnershipVehicle.Group.Api.Services.VotingService>();

// Register Fund Service
builder.Services.AddScoped<CoOwnershipVehicle.Group.Api.Contracts.IFundService, CoOwnershipVehicle.Group.Api.Services.FundService>();

// Register HTTP Client for User Service
builder.Services.AddHttpClient<CoOwnershipVehicle.Group.Api.Services.Interfaces.IUserServiceClient, CoOwnershipVehicle.Group.Api.Services.UserServiceClient>(client =>
{
    // Always use Docker service name when running in containers
    // This ensures inter-service communication works correctly
    var userServiceUrl = "http://user-api:8080";
    
    client.BaseAddress = new Uri(userServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    
    // Log to console so it appears in Docker logs
    Console.WriteLine($"[DIAGNOSTIC] User Service BaseAddress configured: {userServiceUrl}");
});

// Register Background Services
builder.Services.AddHostedService<CoOwnershipVehicle.Group.Api.BackgroundServices.SignatureReminderBackgroundService>();
builder.Services.AddHostedService<CoOwnershipVehicle.Group.Api.BackgroundServices.ProposalExpirationBackgroundService>();

// Configure request size limits for file uploads (50MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50MB
    options.ValueLengthLimit = 52428800;
    options.MultipartHeadersLengthLimit = 52428800;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 52428800; // 50MB
});

// Add Entity Framework
var dbParams = EnvironmentHelper.GetDatabaseConnectionParams(builder.Configuration);
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_GROUP", builder.Configuration) ?? "CoOwnershipVehicle_Group";
var connectionString = dbParams.GetConnectionString();

EnvironmentHelper.LogEnvironmentStatus("Group Service", builder.Configuration);
EnvironmentHelper.LogFinalConnectionDetails("Group Service", dbParams.Database, builder.Configuration);

builder.Services.AddDbContext<GroupDbContext>(options =>
    options.UseSqlServer(connectionString,
        b => b.MigrationsAssembly("CoOwnershipVehicle.Group.Api")));

// Add JWT Authentication
var jwtConfig = EnvironmentHelper.GetJwtConfigParams(builder.Configuration);

// Sync JWT settings to configuration
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
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = "role" // Map "role" claim to User.IsInRole()
    };

    Console.WriteLine($"[DIAGNOSTIC_LOG] RoleClaimType is set to: {options.TokenValidationParameters.RoleClaimType}");
});

// Add MassTransit
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqConnectionString = EnvironmentHelper.GetRabbitMqConnection(builder.Configuration);
        cfg.Host(rabbitMqConnectionString);
    });
});

builder.Services.AddAuthorization();

// Add HttpContextAccessor for accessing HTTP context in services
builder.Services.AddHttpContextAccessor();

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

app.Logger.LogInformation($"FileStorage:LocalStoragePath from configuration: {app.Configuration["FileStorage:LocalStoragePath"]}");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Co-Ownership Vehicle Group API");
        c.RoutePrefix = string.Empty; // Makes Swagger available at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication(); // Ensure this is before UseAuthorization
app.UseAuthorization();

app.MapControllers();

// Apply pending migrations
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
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

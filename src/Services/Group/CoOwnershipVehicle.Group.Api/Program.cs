using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services.Implementations;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Force reload of configuration
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

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

var app = builder.Build();

// Log the configured path for debugging
app.Logger.LogInformation($"FileStorage:LocalStoragePath from configuration: {app.Configuration["FileStorage:LocalStoragePath"]}");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

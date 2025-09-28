using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Notification.Api.Hubs;
using CoOwnershipVehicle.Notification.Api.Services;
using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database Configuration
var connectionString = GetEnvironmentVariable("DB_CONNECTION_STRING") ?? 
    $"Server={GetEnvironmentVariable("DB_SERVER") ?? "localhost"};" +
    $"Database={GetEnvironmentVariable("DB_NOTIFICATION") ?? "CoOwnershipVehicle_Notification"};" +
    $"User Id={GetEnvironmentVariable("DB_USER") ?? "sa"};" +
    $"Password={GetEnvironmentVariable("DB_PASSWORD") ?? ""};" +
    $"TrustServerCertificate={GetEnvironmentVariable("DB_TRUST_CERT") ?? "true"};" +
    $"MultipleActiveResultSets={GetEnvironmentVariable("DB_MULTIPLE_ACTIVE_RESULTS") ?? "true"};";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("CoOwnershipVehicle.Notification.Api")));

// JWT Authentication
var jwtSecretKey = GetEnvironmentVariable("JWT_SECRET_KEY");
var jwtIssuer = GetEnvironmentVariable("JWT_ISSUER") ?? "CoOwnershipVehicle.Auth.Api";
var jwtAudience = GetEnvironmentVariable("JWT_AUDIENCE") ?? "CoOwnershipVehicleApp";

if (!string.IsNullOrEmpty(jwtSecretKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });
}

// SignalR
builder.Services.AddSignalR();

// MassTransit for RabbitMQ
var rabbitmqConnection = GetEnvironmentVariable("RABBITMQ_CONNECTION");
if (!string.IsNullOrEmpty(rabbitmqConnection))
{
    builder.Services.AddMassTransit(x =>
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitmqConnection);
            cfg.ConfigureEndpoints(context);
        });
        
        // Add consumers
        x.AddConsumer<NotificationCreatedEventConsumer>();
        x.AddConsumer<BulkNotificationEventConsumer>();
    });
}

// Services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
builder.Services.AddHostedService<NotificationSchedulerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Co-Ownership Vehicle Notification API");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");

app.Run();

// Helper function to get environment variables with fallback
static string? GetEnvironmentVariable(string name)
{
    return Environment.GetEnvironmentVariable(name) ?? 
           Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) ??
           Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
}

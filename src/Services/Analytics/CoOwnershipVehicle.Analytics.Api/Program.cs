using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Shared.Configuration;
using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database Configuration
var dbParams = EnvironmentHelper.GetDatabaseConnectionParams(builder.Configuration);
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_ANALYTICS", builder.Configuration) ?? "CoOwnershipVehicle_Analytics";
var connectionString = EnvironmentHelper.GetEnvironmentVariable("DB_CONNECTION_STRING", builder.Configuration) ?? dbParams.GetConnectionString();

EnvironmentHelper.LogEnvironmentStatus("Analytics Service", builder.Configuration);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("CoOwnershipVehicle.Analytics.Api")));

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
builder.Services.AddHostedService<AnalyticsProcessorService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Co-Ownership Vehicle Analytics API");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

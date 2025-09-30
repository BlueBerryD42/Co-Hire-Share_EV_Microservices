using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CoOwnershipVehicle.Notification.Api.Data;
using CoOwnershipVehicle.Notification.Api.Hubs;
using CoOwnershipVehicle.Notification.Api.Services;
using CoOwnershipVehicle.Shared.Configuration;
using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Notification.Api.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Co-Ownership Vehicle Notification API", 
        Version = "v1",
        Description = "Notification management service for the Co-Ownership Vehicle Management System"
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
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_NOTIFICATION", builder.Configuration) ?? "CoOwnershipVehicle_Notification";
var connectionString = EnvironmentHelper.GetEnvironmentVariable("DB_CONNECTION_STRING", builder.Configuration) ?? dbParams.GetConnectionString();

EnvironmentHelper.LogEnvironmentStatus("Notification Service", builder.Configuration);
EnvironmentHelper.LogFinalConnectionDetails("Notification Service", dbParams.Database, builder.Configuration);

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("CoOwnershipVehicle.Notification.Api")));

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

// SignalR
builder.Services.AddSignalR();

// MassTransit for RabbitMQ
var rabbitmqConnection = EnvironmentHelper.GetRabbitMqConnection(builder.Configuration);
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

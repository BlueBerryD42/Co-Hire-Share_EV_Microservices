using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Shared.Configuration;
using CoOwnershipVehicle.Vehicle.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using CoOwnershipVehicle.Vehicle.Api.Consumers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace CoOwnershipVehicle.Vehicle.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
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
            
            ConfigureServices(builder);
            var app = builder.Build();

            app.UseExceptionHandler(appBuilder =>
            {
                appBuilder.Run(async context =>
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "text/plain";
                    var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
                    if (exceptionHandlerPathFeature?.Error != null)
                    {
                        await context.Response.WriteAsync($"An unhandled exception occurred: {exceptionHandlerPathFeature.Error.Message}\n{exceptionHandlerPathFeature.Error.StackTrace}");
                    }
                    else
                    {
                        await context.Response.WriteAsync("An unhandled exception occurred.");
                    }
                });
            });

            Configure(app);

            // Apply pending migrations
            try
            {
                using (var scope = app.Services.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
                    await context.Database.MigrateAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to apply database migrations: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                // Don't crash - let the app start and handle migrations later if needed
            }
            
            await app.RunAsync();
        }

        public static void ConfigureServices(WebApplicationBuilder builder)
        {
            // Add services to the container.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    // Use camelCase for JSON serialization (to match frontend)
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    // Allow both string and numeric enum values
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Co-Ownership Vehicle Vehicle API",
                    Version = "v1",
                    Description = "Vehicle management service for the Co-Ownership Vehicle Management System"
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
                        new string[] { }
                    }
                });
            });

            // Add HTTP Client for Group Service
            builder.Services.AddHttpClient<IGroupServiceClient, GroupServiceClient>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["GroupService:BaseUrl"] ?? throw new InvalidOperationException("GroupService:BaseUrl not configured"));
            });

            // Add HTTP Client for Booking Service
            builder.Services.AddHttpClient<IBookingServiceClient, BookingServiceClient>(client =>
            {
                client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:BookingApi"] ?? throw new InvalidOperationException("ServiceUrls:BookingApi not configured"));
            });

            // Add HTTP Client for Payment Service
            builder.Services.AddHttpClient<IPaymentServiceClient, PaymentServiceClient>(client =>
            {
                var paymentServiceUrl = builder.Configuration["ServiceUrls:PaymentService"] ?? "https://localhost:61605";
                client.BaseAddress = new Uri(paymentServiceUrl);
            });

            // Add MassTransit for message bus (must be registered before services that use IPublishEndpoint)
            builder.Services.AddMassTransit(x =>
            {
                x.AddConsumer<GroupCreatedConsumer>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(EnvironmentHelper.GetRabbitMqConnection(builder.Configuration));
                    cfg.ConfigureEndpoints(context);
                });
            });

            // Add Maintenance Service
            builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
            builder.Services.AddScoped<IMaintenancePdfService, MaintenancePdfService>();

            // Add Vehicle Statistics Service
            builder.Services.AddScoped<VehicleStatisticsService>();

            // Add Cost Analysis Service
            builder.Services.AddScoped<CostAnalysisService>();

            // Add Member Usage Service
            builder.Services.AddScoped<MemberUsageService>();

            // Add Vehicle Health Score Service
            builder.Services.AddScoped<VehicleHealthScoreService>();

            // Add Entity Framework
            var dbParams = EnvironmentHelper.GetDatabaseConnectionParams(builder.Configuration);
            dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_VEHICLE", builder.Configuration) ?? "CoOwnershipVehicle_Vehicle";
            var connectionString = dbParams.GetConnectionString();

            EnvironmentHelper.LogEnvironmentStatus("Vehicle Service", builder.Configuration);
            EnvironmentHelper.LogFinalConnectionDetails("Vehicle Service", dbParams.Database, builder.Configuration);

            builder.Services.AddDbContext<VehicleDbContext>(options =>
                options.UseSqlServer(connectionString,
                    b => b.MigrationsAssembly("CoOwnershipVehicle.Vehicle.Api")));

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
                    ClockSkew = TimeSpan.Zero,
                    // Use ClaimTypes.Role to match Admin service and JWT token format
                    // This ensures role claims from JWT tokens work correctly
                    RoleClaimType = ClaimTypes.Role
                };

                Console.WriteLine($"[DIAGNOSTIC_LOG] RoleClaimType is set to: {options.TokenValidationParameters.RoleClaimType}");
            });

            builder.Services.AddAuthorization();
        }

        public static void Configure(WebApplication app)
        {
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vehicle API V1");
                    c.RoutePrefix = string.Empty; // Makes Swagger available at root
                });
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Shared.Configuration;
using CoOwnershipVehicle.Vehicle.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using MassTransit;

namespace CoOwnershipVehicle.Vehicle.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
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
            app.Run();
        }

        public static void ConfigureServices(WebApplicationBuilder builder)
        {
            // Add services to the container.
            builder.Services.AddControllers();
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
                    RoleClaimType = "role" // Map "role" claim to User.IsInRole()
                };

                // DIAGNOSTIC LOG: Print the configured RoleClaimType
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

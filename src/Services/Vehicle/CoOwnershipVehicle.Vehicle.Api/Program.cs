using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Shared.Configuration;

var builder = WebApplication.CreateBuilder(args);

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
            new string[] {}
        }
    });
});

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

var app = builder.Build();

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

// ===== TEMPORARY FIX: Force drop the problematic foreign key on startup =====
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<VehicleDbContext>();
        var dropConstraintSql = @"
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Vehicles_OwnershipGroups_GroupId')
            BEGIN
                ALTER TABLE dbo.Vehicles DROP CONSTRAINT FK_Vehicles_OwnershipGroups_GroupId;
            END";
        dbContext.Database.ExecuteSqlRaw(dropConstraintSql);
        Console.WriteLine("[TEMP_FIX] Successfully executed command to drop FK constraint.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[TEMP_FIX] Could not drop FK constraint: {ex.Message}");
}
// ===== END TEMPORARY FIX =====

app.Run();

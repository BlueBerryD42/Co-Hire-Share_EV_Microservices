using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CoOwnershipVehicle.Payment.Api.Data;
using CoOwnershipVehicle.Payment.Api.Services;
using CoOwnershipVehicle.Shared.Configuration;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var dbParams = EnvironmentHelper.GetDatabaseConnectionParams(builder.Configuration);
dbParams.Database = EnvironmentHelper.GetEnvironmentVariable("DB_PAYMENT", builder.Configuration) ?? "CoOwnershipVehicle_Payment";
var connectionString = dbParams.GetConnectionString();

EnvironmentHelper.LogEnvironmentStatus("Payment Service", builder.Configuration);
EnvironmentHelper.LogFinalConnectionDetails("Payment Service", dbParams.Database, builder.Configuration);

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(connectionString,
        b => b.MigrationsAssembly("CoOwnershipVehicle.Payment.Api")));

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
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(EnvironmentHelper.GetRabbitMqConnection(builder.Configuration));
        cfg.ConfigureEndpoints(context);
    });
});

// Add application services
builder.Services.AddScoped<IVnPayService, VnPayService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Co-Ownership Vehicle Payment API", 
        Version = "v1",
        Description = "Payment processing service with VNPay integration for the Co-Ownership Vehicle Management System"
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment API V1");
        c.RoutePrefix = string.Empty; // Makes Swagger available at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    
    // Ensure database is created
    await context.Database.EnsureCreatedAsync();
}

app.Run();

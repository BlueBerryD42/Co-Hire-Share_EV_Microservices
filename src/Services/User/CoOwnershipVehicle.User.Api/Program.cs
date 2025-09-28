using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.User.Api.Services;
using CoOwnershipVehicle.User.Api.Consumers;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = $"Server={Environment.GetEnvironmentVariable("DB_SERVER")};Database={Environment.GetEnvironmentVariable("DB_USER_SERVICE")};User Id={Environment.GetEnvironmentVariable("DB_USER")};Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};TrustServerCertificate={Environment.GetEnvironmentVariable("DB_TRUST_CERT")};MultipleActiveResultSets={Environment.GetEnvironmentVariable("DB_MULTIPLE_ACTIVE_RESULTS")}";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString,
        b => b.MigrationsAssembly("CoOwnershipVehicle.User.Api")));

// Add Identity services (for UserManager)
builder.Services.AddIdentity<CoOwnershipVehicle.Domain.Entities.User, IdentityRole<Guid>>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
        
        // User settings
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Add JWT Authentication
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? throw new InvalidOperationException("JWT SecretKey not configured");
var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "CoOwnershipVehicle.Auth.Api";
var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "CoOwnershipVehicleApp";

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add MassTransit for message bus
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserRegisteredConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION") ?? "amqp://guest:guest@localhost:5672/");
        
        cfg.ReceiveEndpoint("user-service", e =>
        {
            e.ConfigureConsumer<UserRegisteredConsumer>(context);
        });
        
        cfg.ConfigureEndpoints(context);
    });
});

// Add application services
builder.Services.AddScoped<IUserService, CoOwnershipVehicle.User.Api.Services.UserService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Co-Ownership Vehicle User API", 
        Version = "v1",
        Description = "User profile and KYC management service for the Co-Ownership Vehicle Management System"
    });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "User API V1");
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
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<CoOwnershipVehicle.Domain.Entities.User>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    
    // Ensure database is created
    await context.Database.EnsureCreatedAsync();
    
    // Seed initial data
    await CoOwnershipVehicle.Data.Seeding.DataSeeder.SeedAsync(context, userManager, roleManager);
}

app.Run();

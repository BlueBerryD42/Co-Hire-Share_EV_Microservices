using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework
var connectionString = $"Server={Environment.GetEnvironmentVariable("DB_SERVER")};Database={Environment.GetEnvironmentVariable("DB_VEHICLE")};User Id={Environment.GetEnvironmentVariable("DB_USER")};Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};TrustServerCertificate={Environment.GetEnvironmentVariable("DB_TRUST_CERT")};MultipleActiveResultSets={Environment.GetEnvironmentVariable("DB_MULTIPLE_ACTIVE_RESULTS")}";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString,
        b => b.MigrationsAssembly("CoOwnershipVehicle.Vehicle.Api")));

var app = builder.Build();

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

using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog();

// Add configuration for Ocelot
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Global rate limiting
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1)
            }));

    // API-specific rate limiting
    options.AddFixedWindowLimiter("ApiPolicy", options =>
    {
        options.PermitLimit = 100;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 10;
    });

    // VNPay callback rate limiting (more lenient)
    options.AddFixedWindowLimiter("VNPayPolicy", options =>
    {
        options.PermitLimit = 1000;
        options.Window = TimeSpan.FromMinutes(1);
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Please try again later.", token);
    };
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

// Add Ocelot
builder.Services.AddOcelot();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("auth-service", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddCheck("user-service", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddCheck("group-service", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddCheck("vehicle-service", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddCheck("booking-service", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddCheck("payment-service", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'");
    
    await next();
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Rate limiting middleware
app.UseRateLimiter();

// Authentication middleware
app.UseAuthentication();

// Request logging
app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    Log.Information("API Gateway Request: {Method} {Path} from {IP}", 
        context.Request.Method, 
        context.Request.Path, 
        context.Connection.RemoteIpAddress);
    
    await next();
    
    var duration = DateTime.UtcNow - start;
    Log.Information("API Gateway Response: {StatusCode} in {Duration}ms", 
        context.Response.StatusCode, 
        duration.TotalMilliseconds);
});

// Health check endpoint
app.MapHealthChecks("/health");

// API Gateway status endpoint
app.MapGet("/status", () => new
{
    Status = "API Gateway is running",
    Version = "1.0.0",
    Timestamp = DateTime.UtcNow,
    Services = new
    {
        AuthService = "https://localhost:5001",
        UserService = "https://localhost:5002",
        GroupService = "https://localhost:5003",
        VehicleService = "https://localhost:5004",
        PaymentService = "https://localhost:5005",
        BookingService = "https://localhost:5006"
    }
});

// Documentation endpoint
app.MapGet("/", () => Results.Content(
    """
    <!DOCTYPE html>
    <html>
    <head>
        <title>Co-Ownership Vehicle API Gateway</title>
        <style>
            body { font-family: Arial, sans-serif; margin: 40px; background: #f5f5f5; }
            .container { background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
            h1 { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; }
            .service { background: #ecf0f1; padding: 15px; margin: 10px 0; border-radius: 5px; }
            .endpoint { color: #27ae60; font-weight: bold; }
            .method { color: #e74c3c; font-weight: bold; }
        </style>
    </head>
    <body>
        <div class="container">
            <h1>🚗 Co-Ownership Vehicle API Gateway</h1>
            <p>Welcome to the Co-Ownership Vehicle Management System API Gateway. This gateway provides unified access to all microservices.</p>
            
            <h2>🔗 Available Services</h2>
            
            <div class="service">
                <h3>🔐 Auth Service (Port 5001)</h3>
                <p><span class="endpoint">/api/auth/*</span></p>
                <p>User registration, login, JWT token management</p>
            </div>
            
            <div class="service">
                <h3>👤 User Service (Port 5002)</h3>
                <p><span class="endpoint">/api/user/*</span></p>
                <p>User profiles, KYC documents, profile management</p>
            </div>
            
            <div class="service">
                <h3>👥 Group Service (Port 5003)</h3>
                <p><span class="endpoint">/api/group/*</span></p>
                <p>Ownership groups, member management, shares</p>
            </div>
            
            <div class="service">
                <h3>🚗 Vehicle Service (Port 5004)</h3>
                <p><span class="endpoint">/api/vehicle/*</span></p>
                <p>Vehicle management, availability checking</p>
            </div>
            
            <div class="service">
                <h3>💰 Payment Service (Port 5005)</h3>
                <p><span class="endpoint">/api/payment/*</span></p>
                <p>Expenses, invoices, VNPay integration</p>
            </div>
            
            <div class="service">
                <h3>📅 Booking Service (Port 5006)</h3>
                <p><span class="endpoint">/api/booking/*</span></p>
                <p>Vehicle bookings, priority algorithms, calendar</p>
            </div>
            
            <h2>🔍 Monitoring</h2>
            <ul>
                <li><a href="/health">Health Checks</a></li>
                <li><a href="/status">Gateway Status</a></li>
            </ul>
            
            <h2>🇻🇳 VNPay Integration</h2>
            <p>Full Vietnamese payment gateway integration with support for all local banks and e-wallets.</p>
            
            <h2>🛡️ Security Features</h2>
            <ul>
                <li>JWT Authentication</li>
                <li>Rate Limiting (100 req/min per endpoint)</li>
                <li>CORS Protection</li>
                <li>Security Headers</li>
            </ul>
        </div>
    </body>
    </html>
    """, "text/html"));

// Use Ocelot middleware
await app.UseOcelot();

app.Run();

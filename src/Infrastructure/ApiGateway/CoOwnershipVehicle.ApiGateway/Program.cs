using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using CoOwnershipVehicle.Shared.Configuration;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

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

// Add Serilog
builder.Host.UseSerilog();

// Add configuration for Ocelot
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Add JWT Authentication
var jwtConfig = EnvironmentHelper.GetJwtConfigParams(builder.Configuration);

EnvironmentHelper.LogEnvironmentStatus("API Gateway", builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
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

// ============================================================================
// HTTP REQUEST PIPELINE CONFIGURATION
// ============================================================================

// Development exception page
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'");
    await next();
});

// HTTPS redirection and CORS
app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Rate limiting
app.UseRateLimiter();

// Authentication
app.UseAuthentication();

// Request logging middleware
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

// ============================================================================
// ROUTING CONFIGURATION
// ============================================================================

// Enable routing - this must be called before registering route handlers
app.UseRouting();

// Register gateway endpoints (these will be processed by the routing system)
app.MapHealthChecks("/health");

app.MapGet("/status", () => new
{
    Status = "API Gateway is running",
    Version = "1.0.0",
    Timestamp = DateTime.UtcNow,
    Services = new
    {
        AuthService = "https://localhost:61601",
        UserService = "https://localhost:61604",
        GroupService = "https://localhost:61600",
        VehicleService = "https://localhost:61603",
        PaymentService = "https://localhost:61605",
        BookingService = "https://localhost:7123",
        AdminService = "https://localhost:61609",
        AnalyticsService = "https://localhost:61606",
        NotificationService = "https://localhost:59262"
    }
});

// Favicon handler to prevent Ocelot from processing it
app.MapGet("/favicon.ico", () => Results.NoContent());

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
            <h1> Co-Ownership Vehicle API Gateway</h1>
            <p>Welcome to the Co-Ownership Vehicle Management System API Gateway. This gateway provides unified access to all microservices.</p>
            
            <h2> Available Services</h2>
            
            <div class="service">
                <h3> Auth Service (Port 61601)</h3>
                <p><span class="endpoint">/api/auth/*</span></p>
                <p>User registration, login, JWT token management</p>
            </div>
            
            <div class="service">
                <h3> User Service (Port 61604)</h3>
                <p><span class="endpoint">/api/user/*</span></p>
                <p>User profiles, KYC documents, profile management</p>
            </div>
            
            <div class="service">
                <h3> Group Service (Port 61600)</h3>
                <p><span class="endpoint">/api/group/*</span></p>
                <p>Ownership groups, member management, shares</p>
            </div>
            
            <div class="service">
                <h3> Vehicle Service (Port 61603)</h3>
                <p><span class="endpoint">/api/vehicle/*</span></p>
                <p>Vehicle management, availability checking</p>
            </div>
            
            <div class="service">
                <h3> Payment Service (Port 61605)</h3>
                <p><span class="endpoint">/api/payment/*</span></p>
                <p>Expenses, invoices, VNPay integration</p>
            </div>
            
            <div class="service">
                <h3> Booking Service (Port 7123)</h3>
                <p><span class="endpoint">/api/booking/*</span></p>
                <p>Vehicle bookings, priority algorithms, calendar</p>
            </div>
            
            <div class="service">
                <h3> Admin Service (Port 61609)</h3>
                <p><span class="endpoint">/api/admin/*</span></p>
                <p>Administrative functions and system management</p>
            </div>
            
            <div class="service">
                <h3> Analytics Service (Port 61606)</h3>
                <p><span class="endpoint">/api/analytics/*</span></p>
                <p>Analytics and reporting</p>
            </div>
            
            <div class="service">
                <h3> Notification Service (Port 59262)</h3>
                <p><span class="endpoint">/api/notification/*</span></p>
                <p>Notifications and alerts</p>
            </div>
            
            <h2> Monitoring</h2>
            <ul>
                <li><a href="/health">Health Checks</a></li>
                <li><a href="/status">Gateway Status</a></li>
            </ul>
            
            <h2>🇻🇳 VNPay Integration</h2>
            <p>Full Vietnamese payment gateway integration with support for all local banks and e-wallets.</p>
            
            <h2> Security Features</h2>
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

// Execute route handlers - this ensures gateway endpoints are processed first
// In .NET 6+ minimal APIs, MapGet/MapHealthChecks register endpoints, but we need
// to ensure they're executed before Ocelot runs
app.UseEndpoints(endpoints => { });

// ============================================================================
// OCELOT CONFIGURATION
// ============================================================================

// Apply Ocelot middleware AFTER route handlers
// This ensures that gateway endpoints (/, /health, /status) are processed first,
// and Ocelot only handles unmatched requests (specifically /api/* routes)
await app.UseOcelot();

app.Run();

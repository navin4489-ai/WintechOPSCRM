using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using ORO_POS.Infrastructure.Data;
using ORO_POS.Infrastructure.Services;
using ORO_POS.Core.Interfaces;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// DbContext — PostgreSQL on Render. DATABASE_URL (Render's convention) wins over
// the appsettings default so a local dev connection string never overrides it.
var connectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No database connection string configured. Set DATABASE_URL or ConnectionStrings:DefaultConnection.");

// Render supplies postgres://user:pass@host:port/db — Npgsql accepts this URI form directly.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ORO_POS_Super_Secret_Key_32Chars!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ORO_POS",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ORO_POS_Client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IKitchenService, KitchenService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IOutletService, OutletService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ITableService, TableService>();
builder.Services.AddScoped<PermissionSeedService>();

// CORS - allow Angular dev and production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p =>
    {
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "ORO POS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        { new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// Auto-migrate database on startup (creates DB + schema if missing).
// Set RESET_DB_ON_START=true once to wipe all data and reseed from scratch.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ORO_POS.Infrastructure.Data.AppDbContext>();
    var reset = string.Equals(
        Environment.GetEnvironmentVariable("RESET_DB_ON_START"), "true",
        StringComparison.OrdinalIgnoreCase);
    const int maxAttempts = 5;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            if (reset)
            {
                await db.Database.EnsureDeletedAsync();
                Console.WriteLine("[Startup] RESET_DB_ON_START=true — existing database dropped.");
            }
            await db.Database.MigrateAsync();
            Console.WriteLine("[Startup] Database migration applied.");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] Database migration attempt {attempt}/{maxAttempts} failed: {ex.Message}");
            if (attempt >= maxAttempts) throw;
            await Task.Delay(TimeSpan.FromSeconds(attempt * 5));
        }
    }
}

// Seed database on startup
using (var scope = app.Services.CreateScope())
{
    var seed = scope.ServiceProvider.GetRequiredService<PermissionSeedService>();
    await seed.SeedRolesAndPermissionsAsync();
    await seed.SeedDefaultOutletAndAdminAsync();
}

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Render terminates TLS at its edge proxy; redirecting here would loop.
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Global error handling middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (UnauthorizedAccessException ex)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"success\":false,\"errors\":[\"{ex.Message}\"]}}");
    }
    catch (KeyNotFoundException ex)
    {
        context.Response.StatusCode = 404;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"success\":false,\"errors\":[\"{ex.Message}\"]}}");
    }
    catch (InvalidOperationException ex)
    {
        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"success\":false,\"errors\":[\"{ex.Message}\"]}}");
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"success\":false,\"errors\":[\"Internal server error: {ex.Message}\"]}}");
    }
});

// Liveness probe for Render health checks
app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }));

// Serve the Angular SPA (wwwroot) for everything that is not an API route
var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
Directory.CreateDirectory(wwwroot); // PhysicalFileProvider throws if the directory is absent
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(wwwroot) });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(wwwroot) });

app.MapControllers();

// SPA fallback: unknown non-/api paths return index.html so Angular routes work on refresh
app.MapFallback(async (HttpContext ctx) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        await ctx.Response.WriteAsync("{\"success\":false,\"errors\":[\"Not found\"]}");
        return Results.Empty;
    }

    var index = Path.Combine(wwwroot, "index.html");
    if (!File.Exists(index)) return Results.NotFound();
    ctx.Response.ContentType = "text/html; charset=utf-8";
    return Results.File(index, "text/html; charset=utf-8");
});

app.Run();

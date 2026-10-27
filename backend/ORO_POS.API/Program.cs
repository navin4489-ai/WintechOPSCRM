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
// Render hands out postgres:// URIs; Npgsql expects key=value pairs, so normalize.
var rawConnectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No database connection string configured. Set DATABASE_URL or ConnectionStrings:DefaultConnection.");

var connectionString = ToNpgsqlConnectionString(rawConnectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Converts postgres://user:pass@host:port/db?sslmode=require into the
// key=value form Npgsql requires. Strings already in key=value form pass through.
static string ToNpgsqlConnectionString(string value)
{
    value = value.Trim();
    if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return value;
    }

    var uri = new Uri(value);
    var cs = new StringBuilder();
    cs.Append("Host=").Append(uri.Host);

    if (!uri.IsDefaultPort)
    {
        cs.Append(";Port=").Append(uri.Port);
    }

    var database = uri.AbsolutePath.Trim('/');
    if (database.Length > 0)
    {
        cs.Append(";Database=").Append(Quote(database));
    }

    if (!string.IsNullOrEmpty(uri.UserInfo))
    {
        var separator = uri.UserInfo.IndexOf(':');
        var user = separator >= 0 ? uri.UserInfo[..separator] : uri.UserInfo;
        var password = separator >= 0 ? uri.UserInfo[(separator + 1)..] : string.Empty;
        cs.Append(";Username=").Append(Quote(Uri.UnescapeDataString(user)));
        cs.Append(";Password=").Append(Quote(Uri.UnescapeDataString(password)));
    }

    var query = uri.Query.TrimStart('?');
    foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var kv = pair.Split('=', 2);
        var key = Uri.UnescapeDataString(kv[0]);
        var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;

        if (string.Equals(key, "sslmode", StringComparison.OrdinalIgnoreCase))
        {
            // Render's managed Postgres expects TLS; map to Npgsql's enum spelling.
            cs.Append(";SSL Mode=").Append(val.Equals("disable", StringComparison.OrdinalIgnoreCase)
                ? "Disable"
                : "Require");
        }
        else if (!string.IsNullOrWhiteSpace(key))
        {
            cs.Append(';').Append(key).Append('=').Append(Quote(val));
        }
    }

    return cs.ToString();
}

static string Quote(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

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
builder.Services.AddScoped<IUserService, UserService>();
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
                // Drop the schema rather than the database itself: DROP DATABASE
                // fails while our own pooled connections are open, whereas a schema
                // drop always succeeds and clears every table (incl. migrations).
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;";
                    await cmd.ExecuteNonQueryAsync();
                }
                Console.WriteLine("[Startup] RESET_DB_ON_START=true — schema dropped, all data cleared.");
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

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using CVWebsite.Data;
using CVWebsite.Services;

// ── QuestPDF license (Community = free for small projects) ─────────────────
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.Local.json nếu có (local dev, gitignored — không lên Railway)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.AddControllers();

// ── Database ─────────────────────────────────────────────────────────────────
// Ưu tiên: DATABASE_URL (Railway PostgreSQL) → SQL Server → SQLite
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    // Railway: DATABASE_URL = postgres://user:pass@host:port/db
    var uri     = new Uri(databaseUrl);
    var info    = uri.UserInfo.Split(':');
    var pgConn  = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};"
                + $"Username={info[0]};Password={info[1]};SSL Mode=Disable;Timezone=UTC";
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(pgConn));
    Console.WriteLine("[DB] Using PostgreSQL (Railway)");
}
else
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
               ?? "Data Source=cvwebsite.db";

    // SQL Server (local SSMS)
    if (connStr.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
        connStr.Contains("Data Source=tcp:", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(connStr));
        Console.WriteLine("[DB] Using SQL Server (local)");
    }
    else
    {
        // SQLite — dùng /tmp trong container (luôn writable), máy local dùng thư mục hiện tại
        if (connStr == "Data Source=cvwebsite.db" &&
            Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT") != null)
        {
            connStr = "Data Source=/tmp/cvwebsite.db";
        }
        builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connStr));
        Console.WriteLine($"[DB] Using SQLite: {connStr}");
    }
}

// ── JWT Authentication ───────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
             ?? throw new InvalidOperationException("Jwt:Secret must be set in config or environment.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CVWebsite";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer           = true,
            ValidIssuer              = jwtIssuer,
            ValidateAudience         = false,
            ClockSkew                = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// ── App services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<PdfService>();
builder.Services.AddSingleton<PayOSService>();
builder.Services.AddSingleton<JwtService>();

var app = builder.Build();

// ── Auto-migrate on startup (background so /health responds immediately) ─────
_ = Task.Run(async () =>
{
    await Task.Delay(500); // let the host fully start first
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        Console.WriteLine("[DB] Migration complete.");

        // PostgreSQL: fix DateTime columns created as 'text' by SQLite migration
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL")))
        {
            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'Users'
                          AND column_name  = 'CreatedAt'
                          AND data_type    = 'text'
                    ) THEN
                        ALTER TABLE "Users"
                            ALTER COLUMN "CreatedAt"       TYPE timestamp with time zone
                                USING "CreatedAt"::timestamp with time zone,
                            ALTER COLUMN "PlanActivatedAt" TYPE timestamp with time zone
                                USING "PlanActivatedAt"::timestamp with time zone;
                        ALTER TABLE "PendingPayments"
                            ALTER COLUMN "CreatedAt" TYPE timestamp with time zone
                                USING "CreatedAt"::timestamp with time zone;
                        RAISE NOTICE 'Fixed DateTime columns from text to timestamptz';
                    END IF;
                END $$;
                """);
            Console.WriteLine("[DB] Column type check done.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB ERROR] {ex.GetType().Name}: {ex.Message}");
    }
});

// ── Static files (no browser cache) ─────────────────────────────────────────
var noCache = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        ctx.Context.Response.Headers["Pragma"]        = "no-cache";
        ctx.Context.Response.Headers["Expires"]       = "0";
    }
};
// ── Global JSON error handler ─────────────────────────────────────────────────
app.UseExceptionHandler(err => err.Run(async ctx =>
{
    var ex  = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var msg = ex?.Message ?? "Internal server error.";
    Console.WriteLine($"[EXCEPTION] {ex?.GetType().Name}: {msg}\n{ex?.StackTrace}");
    ctx.Response.StatusCode  = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = msg }));
}));

app.UseDefaultFiles();
app.UseStaticFiles(noCache);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Health check endpoint (Railway / Docker) ─────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

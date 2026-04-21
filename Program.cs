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

builder.Services.AddControllers();

// ── Database ────────────────────────────────────────────────────────────────
// Railway provides DATABASE_URL as postgres://user:pass@host:port/db
// Local dev falls back to SQLite.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    // Railway: DATABASE_URL = postgres://user:pass@host:port/db
    var uri  = new Uri(databaseUrl);
    var info = uri.UserInfo.Split(':');
    var connStr = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};"
                + $"Username={info[0]};Password={info[1]};SSL Mode=Require;Trust Server Certificate=true";
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));
}
else
{
    // Local: SQL Server (SSMS) via appsettings.json
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
               ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not set.");
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(connStr));
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

// ── Auto-migrate on startup ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

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
app.UseDefaultFiles();
app.UseStaticFiles(noCache);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Health check endpoint (Railway / Docker) ─────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

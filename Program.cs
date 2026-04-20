using QuestPDF.Infrastructure;
using CVWebsite.Services;

// ── QuestPDF license (Community = free for small projects) ─────────────────
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Register the PDF service as a singleton (stateless helper)
builder.Services.AddSingleton<PdfService>();

var app = builder.Build();

// Serve index.html and static assets from wwwroot/
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.MapControllers();

app.Run();

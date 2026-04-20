using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CVWebsite.Models;

namespace CVWebsite.Services;

/// <summary>
/// Generates a professional CV PDF.
/// Supports two templates (Modern / Classic) and four colour themes.
/// </summary>
public class PdfService
{
    // ── Theme palette ────────────────────────────────────────────────────────
    private record ThemePalette(
        string Accent,      // main colour (sidebar bg, section titles)
        string AccentLight, // light tint (skill chips, backgrounds)
        string Track,       // progress-bar empty track
        string SideText     // text on top of Accent background
    );

    private static ThemePalette GetPalette(string theme) => theme switch
    {
        "dark"    => new("#0f172a", "#e2e8f0", "#334155", "#f1f5f9"),
        "purple"  => new("#7c3aed", "#ede9fe", "#9d6cf5", "#ffffff"),
        "emerald" => new("#059669", "#d1fae5", "#34d399", "#ffffff"),
        _         => new("#1d4ed8", "#dbeafe", "#4272df", "#ffffff")  // blue
    };

    // ── Entry point ──────────────────────────────────────────────────────────
    public byte[] GenerateCvPdf(CvModel cv)
    {
        var pal = GetPalette(cv.Theme);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10).FontColor("#1e293b"));

                if (cv.Template == "classic")
                    BuildClassic(page, cv, pal);
                else
                    BuildModern(page, cv, pal);

                // Footer — free users see an upgrade prompt watermark
                var footerText = cv.IsPremium
                    ? $"CV Builder Pro  ·  Generated {DateTime.Now:MMMM dd, yyyy}"
                    : $"Created with CV Builder Pro (Free)  ·  Upgrade at cvbuilder.pro  ·  {DateTime.Now:dd MMM yyyy}";

                page.Footer()
                    .Height(18)
                    .Background(pal.Accent)
                    .AlignCenter()
                    .AlignMiddle()
                    .Text(footerText)
                    .FontSize(7).FontColor(pal.SideText);
            });
        });

        return document.GeneratePdf();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MODERN TEMPLATE  –  navy/coloured sidebar on left, content on right
    // ══════════════════════════════════════════════════════════════════════════
    private void BuildModern(PageDescriptor page, CvModel cv, ThemePalette pal)
    {
        page.Content().Row(root =>
        {
            // ── Left sidebar ──────────────────────────────────────────────
            root.ConstantItem(190).Background(pal.Accent).Padding(22).Column(sb =>
            {
                // Profile picture (optional)
                TryRenderPhoto(sb, cv.ProfilePicture, 90, circular: true);

                // Name
                sb.Item().PaddingTop(cv.ProfilePicture != null ? 10 : 0).PaddingBottom(3)
                  .Text(cv.FullName).Bold().FontSize(14).FontColor(pal.SideText);

                sb.Item().PaddingBottom(14).LineHorizontal(0.5f).LineColor(Dim(pal.SideText));

                // Contact rows
                ContactRow(sb, "Email",    cv.Email,   pal);
                ContactRow(sb, "Phone",    cv.Phone,   pal);
                ContactRow(sb, "Location", cv.Address, pal);

                // Skills with progress bars
                if (cv.Skills.Any(s => !string.IsNullOrWhiteSpace(s.Name)))
                {
                    SidebarTitle(sb, "SKILLS", pal);
                    foreach (var skill in cv.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
                    {
                        sb.Item().PaddingBottom(9).Column(s =>
                        {
                            // Name + percentage
                            s.Item().Row(r =>
                            {
                                r.RelativeItem().Text(skill.Name).FontSize(8.5f).FontColor(pal.SideText);
                                r.ConstantItem(28).AlignRight()
                                 .Text($"{skill.Level}%").FontSize(7).FontColor(Dim(pal.SideText));
                            });
                            // Progress bar (fill + track)
                            s.Item().PaddingTop(3).Height(5).Row(bar =>
                            {
                                var fill = Math.Clamp(skill.Level, 0, 100);
                                if (fill > 0)  bar.RelativeItem(fill).Background(pal.SideText);
                                if (fill < 100) bar.RelativeItem(100 - fill).Background(pal.Track);
                            });
                        });
                    }
                }
            });

            // ── Right main content ────────────────────────────────────────
            root.RelativeItem().Padding(28).Column(main =>
            {
                // Summary
                if (!string.IsNullOrWhiteSpace(cv.Summary))
                {
                    MainTitle(main, "PROFILE SUMMARY", pal.Accent);
                    main.Item().PaddingBottom(14).Text(cv.Summary).FontSize(9.5f).LineHeight(1.6f);
                }

                // Work Experience
                if (cv.Experience.Any(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                {
                    MainTitle(main, "WORK EXPERIENCE", pal.Accent);
                    foreach (var job in cv.Experience.Where(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                    {
                        main.Item().PaddingBottom(12).Column(j =>
                        {
                            j.Item().Row(r =>
                            {
                                r.RelativeItem().Text(job.JobTitle).Bold().FontSize(11);
                                r.ConstantItem(85).AlignRight()
                                 .Text(job.Period).FontSize(8).FontColor("#64748b");
                            });
                            j.Item().Text(job.Company).Bold().FontSize(9).FontColor(pal.Accent);
                            if (!string.IsNullOrWhiteSpace(job.Description))
                                j.Item().PaddingTop(3)
                                 .Text(job.Description).FontSize(9).LineHeight(1.55f);
                        });
                        main.Item().PaddingBottom(8).LineHorizontal(0.5f).LineColor("#e2e8f0");
                    }
                }

                // Education
                if (cv.Education.Any(e => !string.IsNullOrWhiteSpace(e.Degree)))
                {
                    MainTitle(main, "EDUCATION", pal.Accent);
                    foreach (var edu in cv.Education.Where(e => !string.IsNullOrWhiteSpace(e.Degree)))
                    {
                        main.Item().PaddingBottom(10).Column(e =>
                        {
                            e.Item().Row(r =>
                            {
                                r.RelativeItem().Text(edu.Degree).Bold().FontSize(10);
                                r.ConstantItem(85).AlignRight()
                                 .Text(edu.Years).FontSize(8).FontColor("#64748b");
                            });
                            e.Item().Text(edu.Institution).FontSize(9).FontColor(pal.Accent);
                        });
                    }
                }
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CLASSIC TEMPLATE  –  full-width header, single-column body
    // ══════════════════════════════════════════════════════════════════════════
    private void BuildClassic(PageDescriptor page, CvModel cv, ThemePalette pal)
    {
        page.Content().Column(root =>
        {
            // ── Header band ───────────────────────────────────────────────
            root.Item().Background(pal.Accent).Padding(28).Row(header =>
            {
                TryRenderPhotoInline(header, cv.ProfilePicture, 75);

                header.RelativeItem().Column(info =>
                {
                    info.Item().Text(cv.FullName).Bold().FontSize(24).FontColor(pal.SideText);
                    info.Item().PaddingTop(8).Row(contacts =>
                    {
                        if (!string.IsNullOrWhiteSpace(cv.Email))
                            contacts.AutoItem().PaddingRight(16)
                                .Text($"✉ {cv.Email}").FontSize(8).FontColor(Dim(pal.SideText));
                        if (!string.IsNullOrWhiteSpace(cv.Phone))
                            contacts.AutoItem().PaddingRight(16)
                                .Text($"📞 {cv.Phone}").FontSize(8).FontColor(Dim(pal.SideText));
                        if (!string.IsNullOrWhiteSpace(cv.Address))
                            contacts.AutoItem()
                                .Text($"📍 {cv.Address}").FontSize(8).FontColor(Dim(pal.SideText));
                    });
                });
            });

            // ── Body ──────────────────────────────────────────────────────
            root.Item().Padding(28).Column(body =>
            {
                // Summary
                if (!string.IsNullOrWhiteSpace(cv.Summary))
                {
                    ClassicTitle(body, "PROFILE SUMMARY", pal.Accent);
                    body.Item().PaddingBottom(14).Text(cv.Summary).FontSize(9.5f).LineHeight(1.6f);
                }

                // Experience
                if (cv.Experience.Any(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                {
                    ClassicTitle(body, "WORK EXPERIENCE", pal.Accent);
                    foreach (var job in cv.Experience.Where(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                    {
                        body.Item().PaddingBottom(12).Column(j =>
                        {
                            j.Item().Row(r =>
                            {
                                r.RelativeItem().Text(job.JobTitle).Bold().FontSize(11);
                                r.ConstantItem(85).AlignRight()
                                 .Text(job.Period).FontSize(8).FontColor("#64748b");
                            });
                            j.Item().Text(job.Company).Bold().FontSize(9).FontColor(pal.Accent);
                            if (!string.IsNullOrWhiteSpace(job.Description))
                                j.Item().PaddingTop(3)
                                 .Text(job.Description).FontSize(9).LineHeight(1.55f);
                        });
                    }
                }

                // Education
                if (cv.Education.Any(e => !string.IsNullOrWhiteSpace(e.Degree)))
                {
                    ClassicTitle(body, "EDUCATION", pal.Accent);
                    foreach (var edu in cv.Education.Where(e => !string.IsNullOrWhiteSpace(e.Degree)))
                    {
                        body.Item().PaddingBottom(8).Row(r =>
                        {
                            r.RelativeItem().Column(e =>
                            {
                                e.Item().Text(edu.Degree).Bold().FontSize(10);
                                e.Item().Text(edu.Institution).FontSize(9).FontColor(pal.Accent);
                            });
                            r.ConstantItem(70).AlignRight().AlignMiddle()
                             .Text(edu.Years).FontSize(8).FontColor("#64748b");
                        });
                    }
                }

                // Skills — two-column grid (avoids AutoItem overflow crash)
                var allSkills = cv.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)).Take(10).ToList();
                if (allSkills.Count > 0)
                {
                    ClassicTitle(body, "SKILLS", pal.Accent);
                    for (int i = 0; i < allSkills.Count; i += 2)
                    {
                        body.Item().PaddingBottom(7).Row(r =>
                        {
                            // Left skill
                            SkillCell(r.RelativeItem(), allSkills[i], pal);
                            r.ConstantItem(14); // gap
                            // Right skill (or empty filler to keep the grid balanced)
                            if (i + 1 < allSkills.Count)
                                SkillCell(r.RelativeItem(), allSkills[i + 1], pal);
                            else
                                r.RelativeItem();
                        });
                    }
                }
            });
        });
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    /// <summary>Renders photo above content in Modern sidebar.</summary>
    private static void TryRenderPhoto(ColumnDescriptor col, string? pic, int size, bool circular)
    {
        if (string.IsNullOrEmpty(pic)) return;
        try
        {
            var bytes = Convert.FromBase64String(
                pic.Contains(',') ? pic.Split(',')[1] : pic);
            col.Item().AlignCenter()
               .Width(size).Height(size)
               .Border(3).BorderColor(Colors.White)
               .Image(bytes).FitArea();
        }
        catch { /* invalid image data – skip silently */ }
    }

    /// <summary>Renders photo inline inside a Row (Classic header).</summary>
    private static void TryRenderPhotoInline(RowDescriptor row, string? pic, int size)
    {
        if (string.IsNullOrEmpty(pic)) return;
        try
        {
            var bytes = Convert.FromBase64String(
                pic.Contains(',') ? pic.Split(',')[1] : pic);
            // ConstantItem defines the column width; let Image fill it naturally
            row.ConstantItem(size + 14)
               .AlignMiddle()
               .Padding(4)
               .Border(2).BorderColor(Colors.White)
               .Image(bytes).FitArea();
        }
        catch { }
    }

    private static void ContactRow(ColumnDescriptor col, string label, string? value, ThemePalette pal)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().PaddingBottom(9).Column(c =>
        {
            c.Item().Text(label.ToUpperInvariant())
             .FontSize(7).Bold().FontColor(Dim(pal.SideText));
            c.Item().Text(value).FontSize(8.5f).FontColor(pal.SideText);
        });
    }

    private static void SidebarTitle(ColumnDescriptor col, string title, ThemePalette pal)
    {
        col.Item().PaddingTop(14).PaddingBottom(4)
           .Text(title).Bold().FontSize(8).FontColor(pal.SideText);
        col.Item().LineHorizontal(0.5f).LineColor(Dim(pal.SideText));
        col.Item().PaddingBottom(8);
    }

    private static void MainTitle(ColumnDescriptor col, string title, string accent)
    {
        col.Item().PaddingBottom(5).Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(8.5f).FontColor(accent);
            c.Item().LineHorizontal(2).LineColor(accent);
        });
        col.Item().PaddingBottom(8);
    }

    /// <summary>Renders a single skill name + progress bar inside a RelativeItem column.</summary>
    private static void SkillCell(IContainer cell, SkillEntry skill, ThemePalette pal)
    {
        cell.Column(s =>
        {
            s.Item().Row(r =>
            {
                r.RelativeItem().Text(skill.Name).FontSize(9);
                r.ConstantItem(28).AlignRight()
                 .Text($"{skill.Level}%").FontSize(8).FontColor("#64748b");
            });
            var fill = Math.Clamp(skill.Level, 0, 100);
            s.Item().Height(4).Row(bar =>
            {
                if (fill > 0)  bar.RelativeItem(fill).Background(pal.Accent);
                if (fill < 100) bar.RelativeItem(100 - fill).Background("#e2e8f0");
            });
        });
    }

    private static void ClassicTitle(ColumnDescriptor col, string title, string accent)
    {
        col.Item().PaddingBottom(5).Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(8.5f).FontColor(accent);
            c.Item().LineHorizontal(1.5f).LineColor(accent);
        });
        col.Item().PaddingBottom(8);
    }

    /// <summary>Returns a "dimmed" version for secondary text on coloured backgrounds.</summary>
    private static string Dim(string color) =>
        color == "#ffffff" ? "#d1d5db" : "#94a3b8";
}

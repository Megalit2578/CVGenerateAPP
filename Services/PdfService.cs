using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CVWebsite.Models;

namespace CVWebsite.Services;

/// <summary>
/// Generates a professional CV PDF.
/// Templates: Modern (free), Classic (free), Executive (pro), Minimal (pro), Elegant (pro), Creative (pro), Sharp (business).
/// Themes: blue, dark, purple, emerald, rose, teal, orange.
/// </summary>
public class PdfService
{
    // ── Theme palette ────────────────────────────────────────────────────────
    private record ThemePalette(
        string Accent,
        string AccentLight,
        string Track,
        string SideText
    );

    private static ThemePalette GetPalette(string theme) => theme switch
    {
        "dark"    => new("#0f172a", "#e2e8f0", "#334155", "#f1f5f9"),
        "purple"  => new("#7c3aed", "#ede9fe", "#9d6cf5", "#ffffff"),
        "emerald" => new("#059669", "#d1fae5", "#34d399", "#ffffff"),
        "rose"    => new("#e11d48", "#ffe4e6", "#fb7185", "#ffffff"),
        "teal"    => new("#0891b2", "#cffafe", "#22d3ee", "#ffffff"),
        "orange"  => new("#ea580c", "#ffedd5", "#fb923c", "#ffffff"),
        _         => new("#1d4ed8", "#dbeafe", "#4272df", "#ffffff") // blue
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

                switch (cv.Template)
                {
                    case "classic":   BuildClassic(page, cv, pal);   break;
                    case "executive": BuildExecutive(page, cv, pal); break;
                    case "minimal":   BuildMinimal(page, cv, pal);   break;
                    case "creative":  BuildCreative(page, cv, pal);  break;
                    case "elegant":   BuildElegant(page, cv, pal);   break;
                    case "sharp":     BuildSharp(page, cv, pal);     break;
                    default:          BuildModern(page, cv, pal);    break;
                }

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
    //  MODERN TEMPLATE  –  coloured sidebar left, content right  [FREE]
    // ══════════════════════════════════════════════════════════════════════════
    private void BuildModern(PageDescriptor page, CvModel cv, ThemePalette pal)
    {
        page.Content().Row(root =>
        {
            root.ConstantItem(190).Background(pal.Accent).Padding(22).Column(sb =>
            {
                TryRenderPhoto(sb, cv.ProfilePicture, 90, circular: true);

                sb.Item().PaddingTop(cv.ProfilePicture != null ? 10 : 0).PaddingBottom(3)
                  .Text(cv.FullName).Bold().FontSize(14).FontColor(pal.SideText);

                sb.Item().PaddingBottom(14).LineHorizontal(0.5f).LineColor(Dim(pal.SideText));

                ContactRow(sb, "Email",    cv.Email,   pal);
                ContactRow(sb, "Phone",    cv.Phone,   pal);
                ContactRow(sb, "Location", cv.Address, pal);

                if (cv.Skills.Any(s => !string.IsNullOrWhiteSpace(s.Name)))
                {
                    SidebarTitle(sb, "SKILLS", pal);
                    foreach (var skill in cv.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
                    {
                        sb.Item().PaddingBottom(9).Column(s =>
                        {
                            s.Item().Row(r =>
                            {
                                r.RelativeItem().Text(skill.Name).FontSize(8.5f).FontColor(pal.SideText);
                                r.ConstantItem(28).AlignRight()
                                 .Text($"{skill.Level}%").FontSize(7).FontColor(Dim(pal.SideText));
                            });
                            s.Item().PaddingTop(3).Height(5).Row(bar =>
                            {
                                var fill = Math.Clamp(skill.Level, 0, 100);
                                if (fill > 0)   bar.RelativeItem(fill).Background(pal.SideText);
                                if (fill < 100) bar.RelativeItem(100 - fill).Background(pal.Track);
                            });
                        });
                    }
                }
            });

            root.RelativeItem().Padding(28).Column(main =>
            {
                if (!string.IsNullOrWhiteSpace(cv.Summary))
                {
                    MainTitle(main, "PROFILE SUMMARY", pal.Accent);
                    main.Item().PaddingBottom(14).Text(cv.Summary).FontSize(9.5f).LineHeight(1.6f);
                }

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
    //  CLASSIC TEMPLATE  –  full-width header, single-column body  [FREE]
    // ══════════════════════════════════════════════════════════════════════════
    private void BuildClassic(PageDescriptor page, CvModel cv, ThemePalette pal)
    {
        page.Content().Column(root =>
        {
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

            root.Item().Padding(28).Column(body =>
            {
                if (!string.IsNullOrWhiteSpace(cv.Summary))
                {
                    ClassicTitle(body, "PROFILE SUMMARY", pal.Accent);
                    body.Item().PaddingBottom(14).Text(cv.Summary).FontSize(9.5f).LineHeight(1.6f);
                }

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

                var allSkills = cv.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)).Take(10).ToList();
                if (allSkills.Count > 0)
                {
                    ClassicTitle(body, "SKILLS", pal.Accent);
                    for (int i = 0; i < allSkills.Count; i += 2)
                    {
                        body.Item().PaddingBottom(7).Row(r =>
                        {
                            SkillCell(r.RelativeItem(), allSkills[i], pal);
                            r.ConstantItem(14);
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

    // ══════════════════════════════════════════════════════════════════════════
    //  EXECUTIVE TEMPLATE  –  bold header, skills panel + content  [PRO]
    // ══════════════════════════════════════════════════════════════════════════
    private void BuildExecutive(PageDescriptor page, CvModel cv, ThemePalette pal)
    {
        page.Content().Column(root =>
        {
            // ── Full-width accent header ──────────────────────────────────
            root.Item().Background(pal.Accent).Padding(26).Row(header =>
            {
                header.RelativeItem().Column(info =>
                {
                    info.Item().Text(cv.FullName).Bold().FontSize(24).FontColor(pal.SideText);

                    // Contact info row
                    info.Item().PaddingTop(10).Row(c =>
                    {
                        if (!string.IsNullOrWhiteSpace(cv.Email))
                            c.AutoItem().PaddingRight(18)
                             .Text($"✉  {cv.Email}").FontSize(8.5f).FontColor(Dim(pal.SideText));
                        if (!string.IsNullOrWhiteSpace(cv.Phone))
                            c.AutoItem().PaddingRight(18)
                             .Text($"☎  {cv.Phone}").FontSize(8.5f).FontColor(Dim(pal.SideText));
                        if (!string.IsNullOrWhiteSpace(cv.Address))
                            c.AutoItem()
                             .Text($"⌖  {cv.Address}").FontSize(8.5f).FontColor(Dim(pal.SideText));
                    });
                });

                // Photo on right
                TryRenderPhotoInline(header, cv.ProfilePicture, 72);
            });

            // ── Body: skills sidebar + main content ───────────────────────
            root.Item().Row(body =>
            {
                // Left: skills panel with light accent background
                body.ConstantItem(195).Background(pal.AccentLight).Padding(22).Column(left =>
                {
                    var skills = cv.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)).ToList();
                    if (skills.Count > 0)
                    {
                        left.Item().PaddingBottom(5)
                            .Text("SKILLS").Bold().FontSize(8).FontColor(pal.Accent);
                        left.Item().PaddingBottom(12).Height(2).Background(pal.Accent);

                        foreach (var skill in skills)
                        {
                            left.Item().PaddingBottom(11).Column(s =>
                            {
                                s.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(skill.Name).FontSize(9);
                                    r.ConstantItem(30).AlignRight()
                                     .Text($"{skill.Level}%").FontSize(7.5f).FontColor("#64748b");
                                });
                                var fill = Math.Clamp(skill.Level, 0, 100);
                                s.Item().PaddingTop(4).Height(5).Row(bar =>
                                {
                                    if (fill > 0)   bar.RelativeItem(fill).Background(pal.Accent);
                                    if (fill < 100) bar.RelativeItem(100 - fill).Background("#d1d5db");
                                });
                            });
                        }
                    }
                });

                // Right: main content
                body.RelativeItem().Padding(26).Column(main =>
                {
                    if (!string.IsNullOrWhiteSpace(cv.Summary))
                    {
                        ExecTitle(main, "PROFILE SUMMARY", pal.Accent);
                        main.Item().PaddingBottom(14).Text(cv.Summary).FontSize(9.5f).LineHeight(1.6f);
                    }

                    if (cv.Experience.Any(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                    {
                        ExecTitle(main, "WORK EXPERIENCE", pal.Accent);
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
                                j.Item().Text(job.Company).FontSize(9.5f).Bold().FontColor(pal.Accent);
                                if (!string.IsNullOrWhiteSpace(job.Description))
                                    j.Item().PaddingTop(4)
                                     .Text(job.Description).FontSize(9).LineHeight(1.55f);
                            });
                            main.Item().PaddingBottom(8).LineHorizontal(0.5f).LineColor("#e2e8f0");
                        }
                    }

                    if (cv.Education.Any(e => !string.IsNullOrWhiteSpace(e.Degree)))
                    {
                        ExecTitle(main, "EDUCATION", pal.Accent);
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
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MINIMAL TEMPLATE  –  clean typography, no sidebar  [PRO]
    // ══════════════════════════════════════════════════════════════════════════
    private void BuildMinimal(PageDescriptor page, CvModel cv, ThemePalette pal)
    {
        page.Content().Padding(36).Column(root =>
        {
            // Name — very large, accent underline
            root.Item().PaddingBottom(4)
                .Text(cv.FullName).Bold().FontSize(26).FontColor("#0f172a");
            root.Item().Height(3).Background(pal.Accent);
            root.Item().PaddingBottom(16);

            // Contact row
            root.Item().PaddingBottom(20).Row(contacts =>
            {
                if (!string.IsNullOrWhiteSpace(cv.Email))
                    contacts.AutoItem().PaddingRight(20)
                     .Text($"✉  {cv.Email}").FontSize(9).FontColor("#475569");
                if (!string.IsNullOrWhiteSpace(cv.Phone))
                    contacts.AutoItem().PaddingRight(20)
                     .Text($"☎  {cv.Phone}").FontSize(9).FontColor("#475569");
                if (!string.IsNullOrWhiteSpace(cv.Address))
                    contacts.AutoItem()
                     .Text($"⌖  {cv.Address}").FontSize(9).FontColor("#475569");
            });

            // Profile photo (optional, aligned left)
            if (!string.IsNullOrEmpty(cv.ProfilePicture))
            {
                try
                {
                    var bytes = Convert.FromBase64String(
                        cv.ProfilePicture.Contains(',') ? cv.ProfilePicture.Split(',')[1] : cv.ProfilePicture);
                    root.Item().PaddingBottom(16).AlignLeft()
                        .Width(80).Height(80)
                        .Border(2).BorderColor(pal.Accent)
                        .Image(bytes).FitArea();
                }
                catch { }
            }

            // Summary
            if (!string.IsNullOrWhiteSpace(cv.Summary))
            {
                MinTitle(root, "PROFILE SUMMARY", pal.Accent);
                root.Item().PaddingBottom(16).Text(cv.Summary).FontSize(9.5f).LineHeight(1.65f).FontColor("#334155");
            }

            // Experience
            if (cv.Experience.Any(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
            {
                MinTitle(root, "WORK EXPERIENCE", pal.Accent);
                foreach (var job in cv.Experience.Where(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                {
                    root.Item().PaddingBottom(13).Column(j =>
                    {
                        j.Item().Row(r =>
                        {
                            r.RelativeItem().Text(job.JobTitle).Bold().FontSize(11);
                            r.ConstantItem(90).AlignRight()
                             .Text(job.Period).FontSize(8).FontColor("#64748b");
                        });
                        j.Item().PaddingTop(1)
                         .Text(job.Company).FontSize(9.5f).FontColor(pal.Accent).Bold();
                        if (!string.IsNullOrWhiteSpace(job.Description))
                            j.Item().PaddingTop(4)
                             .Text(job.Description).FontSize(9).LineHeight(1.55f).FontColor("#475569");
                    });
                }
            }

            // Education
            if (cv.Education.Any(e => !string.IsNullOrWhiteSpace(e.Degree)))
            {
                MinTitle(root, "EDUCATION", pal.Accent);
                foreach (var edu in cv.Education.Where(e => !string.IsNullOrWhiteSpace(e.Degree)))
                {
                    root.Item().PaddingBottom(10).Row(r =>
                    {
                        r.RelativeItem().Column(e =>
                        {
                            e.Item().Text(edu.Degree).Bold().FontSize(10);
                            e.Item().PaddingTop(1).Text(edu.Institution).FontSize(9).FontColor(pal.Accent);
                        });
                        r.ConstantItem(80).AlignRight().AlignMiddle()
                         .Text(edu.Years).FontSize(8).FontColor("#64748b");
                    });
                }
            }

            // Skills — as rounded tag pills
            var allSkills = cv.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)).ToList();
            if (allSkills.Count > 0)
            {
                MinTitle(root, "SKILLS", pal.Accent);
                // Render as 3-column grid
                for (int i = 0; i < allSkills.Count; i += 3)
                {
                    root.Item().PaddingBottom(7).Row(r =>
                    {
                        SkillCell(r.RelativeItem(), allSkills[i], pal);
                        r.ConstantItem(12);
                        if (i + 1 < allSkills.Count) SkillCell(r.RelativeItem(), allSkills[i + 1], pal);
                        else r.RelativeItem();
                        r.ConstantItem(12);
                        if (i + 2 < allSkills.Count) SkillCell(r.RelativeItem(), allSkills[i + 2], pal);
                        else r.RelativeItem();
                    });
                }
            }
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CREATIVE TEMPLATE  –  dark sidebar, bold accents  [BUSINESS]
    // ══════════════════════════════════════════════════════════════════════════
    private void BuildCreative(PageDescriptor page, CvModel cv, ThemePalette pal)
    {
        const string darkBg   = "#0f172a";
        const string darkText = "#f1f5f9";
        const string dimText  = "#94a3b8";

        page.Content().Row(root =>
        {
            // ── Dark left sidebar ─────────────────────────────────────────
            root.ConstantItem(210).Background(darkBg).Column(sb =>
            {
                // Accent top strip
                sb.Item().Height(8).Background(pal.Accent);

                sb.Item().Padding(22).Column(inner =>
                {
                    // Photo
                    TryRenderPhoto(inner, cv.ProfilePicture, 88, circular: false);

                    // Name
                    inner.Item().PaddingTop(cv.ProfilePicture != null ? 12 : 0).PaddingBottom(4)
                         .Text(cv.FullName).Bold().FontSize(15).FontColor(darkText);

                    inner.Item().PaddingBottom(16).Height(2).Background(pal.Accent);

                    // Contact info
                    if (!string.IsNullOrWhiteSpace(cv.Email))
                        inner.Item().PaddingBottom(8).Column(c =>
                        {
                            c.Item().Text("EMAIL").FontSize(7).Bold().FontColor(dimText);
                            c.Item().Text(cv.Email).FontSize(8.5f).FontColor(darkText);
                        });
                    if (!string.IsNullOrWhiteSpace(cv.Phone))
                        inner.Item().PaddingBottom(8).Column(c =>
                        {
                            c.Item().Text("PHONE").FontSize(7).Bold().FontColor(dimText);
                            c.Item().Text(cv.Phone).FontSize(8.5f).FontColor(darkText);
                        });
                    if (!string.IsNullOrWhiteSpace(cv.Address))
                        inner.Item().PaddingBottom(16).Column(c =>
                        {
                            c.Item().Text("LOCATION").FontSize(7).Bold().FontColor(dimText);
                            c.Item().Text(cv.Address).FontSize(8.5f).FontColor(darkText);
                        });

                    // Skills
                    var skills = cv.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)).ToList();
                    if (skills.Count > 0)
                    {
                        inner.Item().PaddingBottom(6).Text("SKILLS").Bold().FontSize(8).FontColor(pal.Accent);
                        inner.Item().PaddingBottom(12).Height(1.5f).Background(pal.Accent);

                        foreach (var skill in skills)
                        {
                            inner.Item().PaddingBottom(10).Column(s =>
                            {
                                s.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(skill.Name).FontSize(8.5f).FontColor(darkText);
                                    r.ConstantItem(28).AlignRight()
                                     .Text($"{skill.Level}%").FontSize(7).FontColor(dimText);
                                });
                                var fill = Math.Clamp(skill.Level, 0, 100);
                                s.Item().PaddingTop(4).Height(4).Row(bar =>
                                {
                                    if (fill > 0)   bar.RelativeItem(fill).Background(pal.Accent);
                                    if (fill < 100) bar.RelativeItem(100 - fill).Background("#1e293b");
                                });
                            });
                        }
                    }
                });
            });

            // ── White right main ──────────────────────────────────────────
            root.RelativeItem().Background("#ffffff").Padding(28).Column(main =>
            {
                if (!string.IsNullOrWhiteSpace(cv.Summary))
                {
                    CreativeTitle(main, "PROFILE SUMMARY", pal.Accent);
                    main.Item().PaddingBottom(14).Text(cv.Summary).FontSize(9.5f).LineHeight(1.6f);
                }

                if (cv.Experience.Any(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                {
                    CreativeTitle(main, "WORK EXPERIENCE", pal.Accent);
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
                            j.Item().PaddingTop(1).Text(job.Company).FontSize(9.5f).Bold().FontColor(pal.Accent);
                            if (!string.IsNullOrWhiteSpace(job.Description))
                                j.Item().PaddingTop(4)
                                 .Text(job.Description).FontSize(9).LineHeight(1.55f);
                        });
                        main.Item().PaddingBottom(8).LineHorizontal(0.5f).LineColor("#e2e8f0");
                    }
                }

                if (cv.Education.Any(e => !string.IsNullOrWhiteSpace(e.Degree)))
                {
                    CreativeTitle(main, "EDUCATION", pal.Accent);
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
                            e.Item().PaddingTop(1).Text(edu.Institution).FontSize(9).FontColor(pal.Accent);
                        });
                    }
                }
            });
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ELEGANT TEMPLATE  –  accent header + light sidebar left  [PRO]
    // ══════════════════════════════════════════════════════════════════════════
    private void BuildElegant(PageDescriptor page, CvModel cv, ThemePalette pal)
    {
        page.Content().Column(root =>
        {
            // Header bar
            root.Item().Background(pal.Accent).Padding(18).Column(hdr =>
            {
                hdr.Item().Text(cv.FullName).Bold().FontSize(22).FontColor(pal.SideText);
                hdr.Item().PaddingTop(5).Height(2).Background(Dim(pal.SideText));
            });

            // Body
            root.Item().Extend().Row(body =>
            {
                // Left sidebar — light bg, contact + skills as pills + education
                body.ConstantItem(180).Background(pal.AccentLight).Padding(13).Column(sb =>
                {
                    TryRenderPhoto(sb, cv.ProfilePicture, 75, false);

                    if (!string.IsNullOrWhiteSpace(cv.Email))
                        ElegantContact(sb, "EMAIL", cv.Email);
                    if (!string.IsNullOrWhiteSpace(cv.Phone))
                        ElegantContact(sb, "PHONE", cv.Phone);
                    if (!string.IsNullOrWhiteSpace(cv.Address))
                        ElegantContact(sb, "LOCATION", cv.Address);

                    if (cv.Skills.Any(s => !string.IsNullOrWhiteSpace(s.Name)))
                    {
                        sb.Item().PaddingTop(12).Text("SKILLS").Bold().FontSize(7.5f).FontColor("#64748b");
                        sb.Item().PaddingTop(5).PaddingBottom(4);
                        foreach (var skill in cv.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
                        {
                            sb.Item().PaddingBottom(8).Column(s =>
                            {
                                s.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(skill.Name).FontSize(8.5f).FontColor("#334155");
                                    r.ConstantItem(28).AlignRight()
                                     .Text($"{skill.Level}%").FontSize(7).FontColor("#64748b");
                                });
                                var fill = Math.Clamp(skill.Level, 0, 100);
                                s.Item().PaddingTop(3).Height(3).Row(bar =>
                                {
                                    if (fill > 0)   bar.RelativeItem(fill).Background(pal.Accent);
                                    if (fill < 100) bar.RelativeItem(100 - fill).Background("#d1d5db");
                                });
                            });
                        }
                    }

                    if (cv.Education.Any(e => !string.IsNullOrWhiteSpace(e.Degree)))
                    {
                        sb.Item().PaddingTop(14).Text("EDUCATION").Bold().FontSize(7.5f).FontColor("#64748b");
                        sb.Item().PaddingTop(3).PaddingBottom(8);
                        foreach (var edu in cv.Education.Where(e => !string.IsNullOrWhiteSpace(e.Degree)))
                        {
                            sb.Item().PaddingBottom(9).Column(e =>
                            {
                                e.Item().Text(edu.Degree).Bold().FontSize(8.5f).FontColor("#1e293b");
                                e.Item().Text(edu.Institution).FontSize(8).FontColor(pal.Accent);
                                e.Item().Text(edu.Years).FontSize(7.5f).FontColor("#94a3b8");
                            });
                        }
                    }
                });

                // Main content
                body.RelativeItem().Background("#ffffff").Padding(16).Column(mc =>
                {
                    if (!string.IsNullOrWhiteSpace(cv.Summary))
                    {
                        mc.Item().Text("HỒ SƠ").Bold().FontSize(7.5f).FontColor(pal.Accent);
                        mc.Item().PaddingTop(4).PaddingBottom(12)
                           .Background(pal.AccentLight).Padding(9)
                           .Text(cv.Summary).FontSize(9).FontColor("#475569").LineHeight(1.55f);
                    }

                    if (cv.Experience.Any(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                    {
                        mc.Item().Text("KINH NGHIỆM LÀM VIỆC").Bold().FontSize(7.5f).FontColor(pal.Accent);
                        mc.Item().PaddingTop(3).PaddingBottom(8);
                        foreach (var job in cv.Experience.Where(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                        {
                            mc.Item().PaddingBottom(10).BorderLeft(2).BorderColor(pal.AccentLight)
                               .PaddingLeft(10).Column(j =>
                            {
                                j.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(job.JobTitle).Bold().FontSize(9.5f).FontColor("#1e293b");
                                    r.ConstantItem(75).AlignRight().Text(job.Period).FontSize(7.5f).FontColor("#94a3b8");
                                });
                                j.Item().Text(job.Company).Bold().FontSize(8.5f).FontColor(pal.Accent);
                                if (!string.IsNullOrWhiteSpace(job.Description))
                                    j.Item().PaddingTop(2).Text(job.Description).FontSize(8.5f).FontColor("#475569").LineHeight(1.4f);
                            });
                        }
                    }
                });
            });
        });
    }

    private static void ElegantContact(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(7).Column(c =>
        {
            c.Item().Text(label).FontSize(7).Bold().FontColor("#94a3b8");
            c.Item().Text(value).FontSize(8).FontColor("#334155");
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SHARP TEMPLATE  –  dark header + two-col timeline experience  [BUSINESS]
    // ══════════════════════════════════════════════════════════════════════════
    private void BuildSharp(PageDescriptor page, CvModel cv, ThemePalette pal)
    {
        page.Content().Column(root =>
        {
            // Dark header
            root.Item().Background("#0f172a").Padding(18).Row(hdr =>
            {
                hdr.RelativeItem().Column(col =>
                {
                    col.Item().Text(cv.FullName).Bold().FontSize(22).FontColor("#f1f5f9");
                    col.Item().PaddingTop(6).Row(accentRow =>
                    {
                        accentRow.ConstantItem(50).Height(2).Background(pal.Accent);
                        accentRow.RelativeItem();
                    });
                    var contacts = new[] { cv.Email, cv.Phone, cv.Address }
                        .Where(c => !string.IsNullOrWhiteSpace(c));
                    if (contacts.Any())
                        col.Item().PaddingTop(7).Text(string.Join("   ", contacts))
                           .FontSize(8).FontColor("#94a3b8");
                });
                TryRenderPhotoInline(hdr, cv.ProfilePicture, 58);
            });

            // Body
            root.Item().Extend().Row(body =>
            {
                // Left — skills + education
                body.ConstantItem(175).Background("#ffffff").BorderRight(1).BorderColor("#f1f5f9").Padding(13).Column(sb =>
                {
                    if (cv.Skills.Any(s => !string.IsNullOrWhiteSpace(s.Name)))
                    {
                        sb.Item().Text("SKILLS").Bold().FontSize(7.5f).FontColor("#94a3b8");
                        sb.Item().PaddingTop(6).PaddingBottom(2);
                        foreach (var skill in cv.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
                        {
                            sb.Item().PaddingBottom(9).Column(s =>
                            {
                                s.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(skill.Name).Bold().FontSize(8).FontColor("#1e293b");
                                    r.ConstantItem(28).AlignRight()
                                     .Text($"{skill.Level}%").FontSize(7.5f).FontColor(pal.Accent);
                                });
                                var fill = Math.Clamp(skill.Level, 0, 100);
                                s.Item().PaddingTop(3).Height(3).Row(bar =>
                                {
                                    if (fill > 0) bar.RelativeItem(fill).Background(pal.Accent);
                                    if (fill < 100) bar.RelativeItem(100 - fill).Background("#f1f5f9");
                                });
                            });
                        }
                    }

                    if (cv.Education.Any(e => !string.IsNullOrWhiteSpace(e.Degree)))
                    {
                        sb.Item().PaddingTop(16).Text("EDUCATION").Bold().FontSize(7.5f).FontColor("#94a3b8");
                        sb.Item().PaddingTop(6).PaddingBottom(2);
                        foreach (var edu in cv.Education.Where(e => !string.IsNullOrWhiteSpace(e.Degree)))
                        {
                            sb.Item().PaddingBottom(9).Padding(8).Background("#f8fafc").Column(e =>
                            {
                                e.Item().Text(edu.Degree).Bold().FontSize(8.5f).FontColor("#1e293b");
                                e.Item().Text(edu.Institution).FontSize(8).FontColor(pal.Accent);
                                e.Item().Text(edu.Years).FontSize(7.5f).FontColor("#94a3b8");
                            });
                        }
                    }
                });

                // Right — summary + experience timeline
                body.RelativeItem().Background("#ffffff").Padding(16).Column(mc =>
                {
                    if (!string.IsNullOrWhiteSpace(cv.Summary))
                    {
                        mc.Item().Text("ABOUT ME").Bold().FontSize(7.5f).FontColor("#94a3b8");
                        mc.Item().PaddingTop(4).PaddingBottom(14)
                           .BorderLeft(3).BorderColor(pal.Accent)
                           .Background("#f8fafc").Padding(9)
                           .Text(cv.Summary).FontSize(8.5f).FontColor("#475569").LineHeight(1.6f);
                    }

                    if (cv.Experience.Any(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                    {
                        mc.Item().PaddingBottom(8).Text("WORK EXPERIENCE").Bold().FontSize(7.5f).FontColor("#94a3b8");
                        foreach (var job in cv.Experience.Where(j => !string.IsNullOrWhiteSpace(j.JobTitle)))
                        {
                            mc.Item().PaddingBottom(12).Row(jrow =>
                            {
                                // Accent left strip (dot replacement)
                                jrow.ConstantItem(6).Background(pal.Accent);
                                jrow.RelativeItem().PaddingLeft(10).Column(j =>
                                {
                                    j.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text(job.JobTitle).Bold().FontSize(9.5f).FontColor("#1e293b");
                                        r.ConstantItem(70).AlignRight().Text(job.Period).FontSize(7.5f).FontColor("#94a3b8");
                                    });
                                    j.Item().Text(job.Company).Bold().FontSize(8.5f).FontColor(pal.Accent);
                                    if (!string.IsNullOrWhiteSpace(job.Description))
                                        j.Item().PaddingTop(2).Text(job.Description).FontSize(8.5f).FontColor("#64748b").LineHeight(1.45f);
                                });
                            });
                        }
                    }
                });
            });
        });
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

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
        catch { }
    }

    private static void TryRenderPhotoInline(RowDescriptor row, string? pic, int size)
    {
        if (string.IsNullOrEmpty(pic)) return;
        try
        {
            var bytes = Convert.FromBase64String(
                pic.Contains(',') ? pic.Split(',')[1] : pic);
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

    private static void ClassicTitle(ColumnDescriptor col, string title, string accent)
    {
        col.Item().PaddingBottom(5).Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(8.5f).FontColor(accent);
            c.Item().LineHorizontal(1.5f).LineColor(accent);
        });
        col.Item().PaddingBottom(8);
    }

    /// <summary>Executive section header — accent left border bar + bold text.</summary>
    private static void ExecTitle(ColumnDescriptor col, string title, string accent)
    {
        col.Item().PaddingBottom(5).Row(r =>
        {
            r.ConstantItem(4).Background(accent);
            r.RelativeItem().PaddingLeft(10).Text(title).Bold().FontSize(9).FontColor(accent);
        });
        col.Item().PaddingBottom(9);
    }

    /// <summary>Minimal section header — title + thin gray underline.</summary>
    private static void MinTitle(ColumnDescriptor col, string title, string accent)
    {
        col.Item().PaddingBottom(5).Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(9).FontColor(accent);
            c.Item().LineHorizontal(1).LineColor("#e2e8f0");
        });
        col.Item().PaddingBottom(10);
    }

    /// <summary>Creative section header — accent left border + larger bold text.</summary>
    private static void CreativeTitle(ColumnDescriptor col, string title, string accent)
    {
        col.Item().PaddingBottom(5).Row(r =>
        {
            r.ConstantItem(4).Background(accent);
            r.RelativeItem().PaddingLeft(10).Text(title).Bold().FontSize(9.5f).FontColor("#0f172a");
        });
        col.Item().PaddingBottom(10);
    }

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
                if (fill > 0)   bar.RelativeItem(fill).Background(pal.Accent);
                if (fill < 100) bar.RelativeItem(100 - fill).Background("#e2e8f0");
            });
        });
    }

    private static string Dim(string color) =>
        color == "#ffffff" ? "#d1d5db" : "#94a3b8";
}

namespace CVWebsite.Models;

/// <summary>Root CV payload sent from the browser as JSON.</summary>
public class CvModel
{
    public string FullName       { get; set; } = string.Empty;
    public string Email          { get; set; } = string.Empty;
    public string Phone          { get; set; } = string.Empty;
    public string Address        { get; set; } = string.Empty;
    public string Summary        { get; set; } = string.Empty;

    /// <summary>Base64 data-URL (e.g. "data:image/jpeg;base64,..."). Optional.</summary>
    public string? ProfilePicture { get; set; }

    /// <summary>Which PDF layout to use: "modern" (two-col) or "classic" (single-col).</summary>
    public string Template       { get; set; } = "modern";

    /// <summary>Colour theme: "blue" | "dark" | "purple" | "emerald".</summary>
    public string Theme          { get; set; } = "blue";

    /// <summary>True when the user has an active Premium subscription.</summary>
    public bool IsPremium        { get; set; } = false;

    public List<EducationEntry> Education  { get; set; } = new();
    public List<SkillEntry>     Skills     { get; set; } = new();
    public List<WorkEntry>      Experience { get; set; } = new();
}

public class EducationEntry
{
    public string Degree      { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string Years       { get; set; } = string.Empty;
}

/// <summary>Skill with a proficiency level (0–100).</summary>
public class SkillEntry
{
    public string Name  { get; set; } = string.Empty;
    public int    Level { get; set; } = 80;
}

public class WorkEntry
{
    public string JobTitle    { get; set; } = string.Empty;
    public string Company     { get; set; } = string.Empty;
    public string Period      { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

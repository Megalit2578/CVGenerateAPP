namespace CVWebsite.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Plan { get; set; } = "free"; // free | pro | business
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PlanActivatedAt { get; set; }
}

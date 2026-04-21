namespace CVWebsite.Models;

public class PendingPayment
{
    public int Id { get; set; }
    public long OrderCode { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Plan { get; set; } = "pro";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Fulfilled { get; set; } = false;
}

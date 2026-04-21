using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CVWebsite.Data;

namespace CVWebsite.Controllers;

/// <summary>
/// DEV-ONLY admin endpoint to inspect database contents.
/// Returns plain JSON — do NOT expose in production without auth.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminController(AppDbContext db) => _db = db;

    /// <summary>GET /api/admin/db — returns all users and pending payments</summary>
    [HttpGet("db")]
    public async Task<IActionResult> ViewDb()
    {
        var users = (await _db.Users.ToListAsync())
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Plan,
                u.CreatedAt,
                u.PlanActivatedAt,
                PasswordHash = u.PasswordHash[..8] + "…" // show only prefix for safety
            })
            .ToList();

        var payments = await _db.PendingPayments
            .Select(p => new
            {
                p.Id,
                p.OrderCode,
                p.UserId,
                p.Plan,
                p.CreatedAt,
                p.Fulfilled
            })
            .ToListAsync();

        return Ok(new
        {
            users,
            pendingPayments = payments,
            _meta = new
            {
                userCount    = users.Count,
                paymentCount = payments.Count,
                generatedAt  = DateTime.UtcNow
            }
        });
    }
}

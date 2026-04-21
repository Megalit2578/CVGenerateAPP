using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CVWebsite.Data;
using CVWebsite.Models;
using CVWebsite.Services;
using System.Text.Json;

namespace CVWebsite.Controllers;

[ApiController]
[Route("api/payment")]
public class PaymentController : ControllerBase
{
    private readonly PayOSService _payOS;
    private readonly AppDbContext _db;
    private readonly JwtService   _jwt;

    public PaymentController(PayOSService payOS, AppDbContext db, JwtService jwt)
    {
        _payOS = payOS;
        _db    = db;
        _jwt   = jwt;
    }

    /// <summary>
    /// POST /api/payment/create-link  [Authorize]
    /// Body: { "plan": "pro" | "business" }
    /// Returns: { "checkoutUrl": "...", "orderCode": 12345, "plan": "pro" }
    /// </summary>
    [HttpPost("create-link")]
    [Authorize]
    public async Task<IActionResult> CreateLink([FromBody] CreateLinkRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Plan))
            return BadRequest(new { error = "Plan is required." });

        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        if (sub == null || !int.TryParse(sub, out var userId))
            return Unauthorized();

        var baseUrl   = $"{Request.Scheme}://{Request.Host}";
        var returnUrl = $"{baseUrl}/payment-success.html";
        var cancelUrl = $"{baseUrl}/";

        try
        {
            var (url, orderCode) = await _payOS.CreatePaymentLinkAsync(req.Plan, returnUrl, cancelUrl);

            _db.PendingPayments.Add(new PendingPayment
            {
                OrderCode = orderCode,
                UserId    = userId,
                Plan      = req.Plan
            });
            await _db.SaveChangesAsync();

            return Ok(new { checkoutUrl = url, orderCode, plan = req.Plan });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex)         { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>
    /// GET /api/payment/verify?orderCode=123  [Authorize]
    /// Called by payment-success.html to confirm payment and upgrade plan.
    /// </summary>
    [HttpGet("verify")]
    [Authorize]
    public async Task<IActionResult> Verify([FromQuery] long orderCode)
    {
        try
        {
            var (paid, status) = await _payOS.GetPaymentStatusAsync(orderCode);

            if (paid)
                await UpgradePlanByOrderCode(orderCode);

            return Ok(new { paid, status });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>
    /// POST /api/payment/webhook
    /// PayOS calls this when payment status changes.
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            Console.WriteLine($"[PayOS Webhook] {body}");

            using var doc  = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("status", out var statusEl) &&
                string.Equals(statusEl.GetString(), "PAID", StringComparison.OrdinalIgnoreCase) &&
                data.TryGetProperty("orderCode", out var codeEl))
            {
                var orderCode = codeEl.GetInt64();
                Console.WriteLine($"[PayOS Webhook] Order {orderCode} PAID — upgrading plan");
                await UpgradePlanByOrderCode(orderCode);
            }

            return Ok(new { code = "00" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PayOS Webhook] Error: {ex.Message}");
            return Ok(new { code = "00" }); // Always 200 to acknowledge
        }
    }

    /// <summary>
    /// GET /api/payment/dev-activate?plan=pro
    /// Dev endpoint — creates/updates dev@cvwebsite.local and returns a JWT via HTML redirect.
    /// </summary>
    [HttpGet("dev-activate")]
    public async Task<IActionResult> DevActivate([FromQuery] string plan = "pro")
    {
        if (!PayOSService.Plans.ContainsKey(plan))
            return BadRequest(new { error = $"Unknown plan: {plan}. Use 'pro' or 'business'." });

        // Find or create the dev account
        var devEmail = "dev@cvwebsite.local";
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == devEmail);
        if (user == null)
        {
            user = new User
            {
                Email        = devEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("devpassword"),
                Plan         = plan
            };
            _db.Users.Add(user);
        }
        else
        {
            user.Plan = plan;
            user.PlanActivatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        var token = _jwt.Generate(user);
        var color = plan == "business" ? "#7c3aed" : "#1d4ed8";
        var label = plan == "business" ? "BUSINESS" : "PRO";

        var html = "<html><head><meta charset='UTF-8'><title>Dev Activate</title>"
            + "<style>body{font-family:sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;background:#f1f5f9}"
            + ".card{background:#fff;padding:2rem;border-radius:12px;box-shadow:0 4px 20px rgba(0,0,0,.1);text-align:center;max-width:360px}"
            + ".badge{display:inline-block;padding:.4rem 1rem;border-radius:20px;font-weight:700;margin-bottom:1rem;"
            + "background:" + color + ";color:#fff}</style></head>"
            + "<body><div class='card'>"
            + "<div class='badge'>" + label + "</div>"
            + "<h3>&#x2705; Dev Activate</h3>"
            + "<p>Plan <strong>" + plan + "</strong> activated for <code>" + devEmail + "</code>.<br>Redirecting in 1s&hellip;</p>"
            + "</div>"
            + "<script>"
            + "localStorage.setItem('cvbJwt','" + token + "');"
            + "setTimeout(function(){location.href='/';},1000);"
            + "</script></body></html>";

        return Content(html, "text/html");
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task UpgradePlanByOrderCode(long orderCode)
    {
        var pending = await _db.PendingPayments
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.OrderCode == orderCode && !p.Fulfilled);

        if (pending?.UserId == null) return;

        var user = pending.User ?? await _db.Users.FindAsync(pending.UserId.Value);
        if (user == null) return;

        user.Plan             = pending.Plan;
        user.PlanActivatedAt  = DateTime.UtcNow;
        pending.Fulfilled     = true;
        await _db.SaveChangesAsync();

        Console.WriteLine($"[Payment] User {user.Email} upgraded to {pending.Plan}");
    }
}

public record CreateLinkRequest(string Plan);

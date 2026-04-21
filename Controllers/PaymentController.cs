using Microsoft.AspNetCore.Mvc;
using CVWebsite.Services;
using System.Text.Json;

namespace CVWebsite.Controllers;

[ApiController]
[Route("api/payment")]
public class PaymentController : ControllerBase
{
    private readonly PayOSService _payOS;

    public PaymentController(PayOSService payOS) => _payOS = payOS;

    /// <summary>
    /// POST /api/payment/create-link
    /// Body: { "plan": "pro" | "business" }
    /// Returns: { "checkoutUrl": "...", "orderCode": 12345, "plan": "pro" }
    /// </summary>
    [HttpPost("create-link")]
    public async Task<IActionResult> CreateLink([FromBody] CreateLinkRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Plan))
            return BadRequest(new { error = "Plan is required." });

        var baseUrl   = $"{Request.Scheme}://{Request.Host}";
        var returnUrl = $"{baseUrl}/payment-success.html";
        var cancelUrl = $"{baseUrl}/";

        try
        {
            var (url, orderCode) = await _payOS.CreatePaymentLinkAsync(req.Plan, returnUrl, cancelUrl);
            return Ok(new { checkoutUrl = url, orderCode, plan = req.Plan });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Exception ex)         { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>
    /// GET /api/payment/verify?orderCode=123
    /// Called by payment-success.html to confirm the payment is actually PAID.
    /// </summary>
    [HttpGet("verify")]
    public async Task<IActionResult> Verify([FromQuery] long orderCode)
    {
        try
        {
            var (paid, status) = await _payOS.GetPaymentStatusAsync(orderCode);
            return Ok(new { paid, status });
        }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }

    /// <summary>
    /// POST /api/payment/webhook
    /// PayOS calls this when payment status changes (PAID, CANCELLED, etc.).
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            // Log the webhook payload (replace with proper logging in production)
            Console.WriteLine($"[PayOS Webhook] {body}");

            // Parse to check status
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // PayOS webhook structure: { "code": "00", "data": { "orderCode": ..., "status": "PAID" } }
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("status", out var statusEl))
            {
                var status = statusEl.GetString() ?? string.Empty;
                if (string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) &&
                    data.TryGetProperty("orderCode", out var codeEl))
                {
                    var orderCode = codeEl.GetInt64();
                    Console.WriteLine($"[PayOS Webhook] Order {orderCode} PAID");
                    // In a production app with a database, you would mark the subscription as active here.
                }
            }

            return Ok(new { code = "00" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PayOS Webhook] Error: {ex.Message}");
            return Ok(new { code = "00" }); // Always return 200 to acknowledge receipt
        }
    }
}

public record CreateLinkRequest(string Plan);

using Microsoft.AspNetCore.Mvc;
using CVWebsite.Services;

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
    /// Returns: { "checkoutUrl": "https://pay.payos.vn/..." }
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
            var url = await _payOS.CreatePaymentLinkAsync(req.Plan, returnUrl, cancelUrl);
            return Ok(new { checkoutUrl = url });
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
}

public record CreateLinkRequest(string Plan);

using Microsoft.AspNetCore.Mvc;
using Net.payOS.Types;
using CVWebsite.Services;

namespace CVWebsite.Controllers;

[ApiController]
[Route("api/payment")]
public class PaymentController : ControllerBase
{
    private readonly PayOSService _payOS;

    public PaymentController(PayOSService payOS) => _payOS = payOS;

    // POST /api/payment/create-link
    // Body: { "plan": "pro" | "business" }
    // Returns: { "checkoutUrl": "https://pay.payos.vn/..." }
    [HttpPost("create-link")]
    public async Task<IActionResult> CreateLink([FromBody] CreateLinkRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Plan))
            return BadRequest(new { error = "Plan is required." });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var returnUrl = $"{baseUrl}/payment-success.html";
        var cancelUrl = $"{baseUrl}/payment-cancel.html";

        try
        {
            var checkoutUrl = await _payOS.CreatePaymentLinkAsync(req.Plan, returnUrl, cancelUrl);
            return Ok(new { checkoutUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET /api/payment/verify?orderCode=123
    // Called by payment-success.html to confirm the payment was actually paid
    [HttpGet("verify")]
    public async Task<IActionResult> Verify([FromQuery] long orderCode)
    {
        try
        {
            var info = await _payOS.GetPaymentInfoAsync(orderCode);
            bool paid = info.status == "PAID";
            return Ok(new { paid, status = info.status });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // POST /api/payment/webhook
    // PayOS calls this automatically when payment status changes
    [HttpPost("webhook")]
    public IActionResult Webhook([FromBody] WebhookType body)
    {
        try
        {
            var data = _payOS.VerifyWebhookData(body);
            // data.orderCode, data.amount, data.description are now verified
            // In a production system you would update a database here
            return Ok(new { success = true });
        }
        catch
        {
            return BadRequest(new { success = false });
        }
    }
}

public record CreateLinkRequest(string Plan);

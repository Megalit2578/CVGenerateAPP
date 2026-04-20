using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;

namespace CVWebsite.Services;

/// <summary>
/// Wraps the payOS v2 SDK for creating payment links and verifying status.
/// Credentials are read from appsettings.json → section "PayOS".
/// </summary>
public class PayOSService
{
    private readonly PayOSClient _client;

    // Plans available for purchase
    public static readonly Dictionary<string, (string Name, int Amount)> Plans = new()
    {
        ["pro"]      = ("CV Builder Pro",      29_000),
        ["business"] = ("CV Builder Business", 99_000),
    };

    public PayOSService(IConfiguration config)
    {
        var s = config.GetSection("PayOS");
        _client = new PayOSClient(new PayOSOptions
        {
            ClientId    = s["ClientId"]!,
            ApiKey      = s["ApiKey"]!,
            ChecksumKey = s["ChecksumKey"]!,
        });
    }

    /// <summary>Creates a PayOS checkout URL.</summary>
    public async Task<string> CreatePaymentLinkAsync(string plan, string returnUrl, string cancelUrl)
    {
        if (!Plans.TryGetValue(plan, out var info))
            throw new ArgumentException($"Unknown plan: {plan}");

        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 10_000_000_000L
                         + new Random().Next(1, 999);

        var request = new CreatePaymentLinkRequest
        {
            OrderCode   = orderCode,
            Amount      = info.Amount,
            Description = info.Name[..Math.Min(info.Name.Length, 25)],
            ReturnUrl   = returnUrl,
            CancelUrl   = cancelUrl,
        };

        var result = await _client.PaymentRequests.CreateAsync(request);
        return result.CheckoutUrl;
    }

    /// <summary>Returns whether an order has been paid.</summary>
    public async Task<(bool Paid, string Status)> GetPaymentStatusAsync(long orderCode)
    {
        var info   = await _client.PaymentRequests.GetAsync(orderCode);
        var status = info.Status.ToString();
        return (status == "PAID", status);
    }
}

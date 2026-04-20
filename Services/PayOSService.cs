using Net.payOS;
using Net.payOS.Types;

namespace CVWebsite.Services;

public class PayOSService
{
    private readonly PayOS _payOS;

    // Plans: key → (displayName, amountVND)
    public static readonly Dictionary<string, (string Name, int Amount)> Plans = new()
    {
        ["pro"]      = ("CV Builder Pro",      29000),
        ["business"] = ("CV Builder Business", 99000),
    };

    public PayOSService(IConfiguration config)
    {
        var section = config.GetSection("PayOS");
        _payOS = new PayOS(
            clientId:    section["ClientId"]!,
            apiKey:      section["ApiKey"]!,
            checksumKey: section["ChecksumKey"]!
        );
    }

    public async Task<string> CreatePaymentLinkAsync(string plan, string returnUrl, string cancelUrl)
    {
        if (!Plans.TryGetValue(plan, out var info))
            throw new ArgumentException($"Unknown plan: {plan}");

        // orderCode must be a unique positive long — use timestamp + small random
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 10_000_000_000L
                         + new Random().Next(0, 1000);

        var items = new List<ItemData> { new ItemData(info.Name, 1, info.Amount) };

        var paymentData = new PaymentData(
            orderCode:   orderCode,
            amount:      info.Amount,
            description: info.Name[..Math.Min(info.Name.Length, 25)],
            items:       items,
            cancelUrl:   cancelUrl,
            returnUrl:   returnUrl
        );

        CreatePaymentResult result = await _payOS.createPaymentLink(paymentData);
        return result.checkoutUrl;
    }

    public async Task<PaymentLinkInformation> GetPaymentInfoAsync(long orderCode)
        => await _payOS.getPaymentLinkInformation(orderCode);

    public WebhookData VerifyWebhookData(WebhookType webhookBody)
        => _payOS.verifyPaymentWebhookData(webhookBody);
}

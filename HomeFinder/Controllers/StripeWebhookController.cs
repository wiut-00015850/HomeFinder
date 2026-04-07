using HomeFinder.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

[Route("stripe/webhook")]
public class StripeWebhookController : Controller
{
    private readonly IConfiguration _cfg;
    private readonly HomeFinderContext _db;

    public StripeWebhookController(IConfiguration cfg, HomeFinderContext db)
    {
        _cfg = cfg;
        _db = db;
        StripeConfiguration.ApiKey = _cfg["Stripe:SecretKey"];
    }

    [HttpPost]
    public async Task<IActionResult> Index()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var sigHeader = Request.Headers["Stripe-Signature"];
        var whSecret = _cfg["Stripe:WebhookSecret"];

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, sigHeader, whSecret);
        }
        catch
        {
            return BadRequest();
        }

        if (stripeEvent.Type == "checkout.session.completed")
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session != null)
            {
                if (int.TryParse(session.ClientReferenceId, out var userId))
                {
                    var existing = await _db.LandlordSubscriptions
                        .FirstOrDefaultAsync(x => x.UserId == userId);

                    if (existing == null)
                    {
                        existing = new HomeFinder.Models.LandlordSubscription
                        {
                            UserId = userId
                        };
                        _db.LandlordSubscriptions.Add(existing);
                    }

                    existing.StripeCustomerId = session.CustomerId;
                    existing.StripeSubscriptionId = session.SubscriptionId;
                    existing.UpdatedAtUtc = DateTime.UtcNow;

                    await _db.SaveChangesAsync();
                }
            }
        }

        if (stripeEvent.Type == "customer.subscription.created" ||
            stripeEvent.Type == "customer.subscription.updated" ||
            stripeEvent.Type == "customer.subscription.deleted")
        {
            var sub = stripeEvent.Data.Object as Stripe.Subscription;
            if (sub != null)
            {
                var record = await _db.LandlordSubscriptions
                    .FirstOrDefaultAsync(x => x.StripeSubscriptionId == sub.Id);

                if (record != null)
                {
                    record.Status = sub.Status;
                    record.UpdatedAtUtc = DateTime.UtcNow;

                    var item = sub.Items?.Data?.FirstOrDefault();
                    if (item != null)
                    {
                        record.CurrentPeriodEndUtc = item.CurrentPeriodEnd;
                    }

                    if (stripeEvent.Type == "customer.subscription.deleted")
                    {
                        record.Status = "canceled";
                    }

                    await _db.SaveChangesAsync();
                }
            }
        }

        return Ok();
    }
}

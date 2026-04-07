using HomeFinder.Context;
using HomeFinder.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace HomeFinder.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IConfiguration _cfg;
        private readonly HomeFinderContext _db;

        public PaymentController(IConfiguration cfg, HomeFinderContext db)
        {
            _cfg = cfg;
            _db = db;
            StripeConfiguration.ApiKey = _cfg["Stripe:SecretKey"];
        }

        public IActionResult Premium()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePremiumCheckoutSession()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var domain = $"{Request.Scheme}://{Request.Host}";
            var priceId = _cfg["Stripe:LandlordSubscriptionPriceId"];

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                ClientReferenceId = userId.Value.ToString(),
                SuccessUrl = domain + "/Payment/Success?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = domain + "/Payment/Cancel",
                LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                Price = priceId,
                Quantity = 1
            }
        }
            };

            var service = new SessionService();
            var session = service.Create(options);

            return Redirect(session.Url);
        }

        public async Task<IActionResult> Success(string session_id)
        {
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(session_id);

            if (!int.TryParse(session.ClientReferenceId, out var userId))
                return BadRequest("ClientReferenceId invalid");

            var record = await _db.LandlordSubscriptions
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (record == null)
            {
                record = new LandlordSubscription
                {
                    UserId = userId
                };
                _db.LandlordSubscriptions.Add(record);
            }

            record.StripeCustomerId = session.CustomerId;
            record.StripeSubscriptionId = session.SubscriptionId;
            record.Status = "active";
            record.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            HttpContext.Session.SetString("IsPremium", "1");

            return View();
        }

        public IActionResult Cancel()
        {
            return View();
        }

        private static bool IsPremiumActive(LandlordSubscription s)
        {
            var statusOk = string.Equals(s.Status, "active", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(s.Status, "trialing", StringComparison.OrdinalIgnoreCase);

            if (!statusOk) return false;

            if (s.CurrentPeriodEndUtc == null) return true;

            return s.CurrentPeriodEndUtc.Value > DateTime.UtcNow;
        }
    }
}

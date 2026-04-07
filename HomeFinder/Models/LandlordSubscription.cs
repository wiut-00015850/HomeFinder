namespace HomeFinder.Models;

public partial class LandlordSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? Status { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

}

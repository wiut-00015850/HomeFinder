namespace HomeFinder.Models;

public class ReviewSummaryViewModel
{
    public string Overview { get; set; } = string.Empty;

    public string RecentTrend { get; set; } = string.Empty;

    public string RatingBreakdown { get; set; } = string.Empty;

    public List<string> PositiveHighlights { get; set; } = new();

    public List<string> NegativeHighlights { get; set; } = new();
}

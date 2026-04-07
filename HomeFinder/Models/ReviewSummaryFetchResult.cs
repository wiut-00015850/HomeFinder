namespace HomeFinder.Models;

public class ReviewSummaryFetchResult
{
    public string Status { get; set; } = "processing";

    public string Message { get; set; } = string.Empty;

    public DateTime? GeneratedAtUtc { get; set; }

    public ReviewSummaryViewModel? Summary { get; set; }

    /// <summary>Краткая техническая причина сбоя (для отладки в F12 / Network). Не для показа обычным пользователям в UI.</summary>
    public string? Diagnostic { get; set; }
}


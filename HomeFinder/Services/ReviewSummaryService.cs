using System.Text.RegularExpressions;
using HomeFinder.Models;

namespace HomeFinder.Services;

public class ReviewSummaryService : IReviewSummaryService
{
    private const int MinimumReviewsForSummary = 4;
    private const int RecentReviewWindow = 3;

    // A compact catalog keeps the summary grounded in apartment-related topics.
    private static readonly ThemeDefinition[] ThemeDefinitions =
    {
        new("Location", "location", "district", "area", "neighborhood", "neighbourhood", "metro", "center", "central", "near", "район", "локац", "место", "располож", "центр", "метро", "рядом"),
        new("Cleanliness", "clean", "tidy", "spotless", "cozy", "cosy", "dirty", "messy", "smell", "odor", "odour", "чист", "уют", "гряз", "запах", "пыль", "аккурат"),
        new("Price", "price", "cost", "expensive", "cheap", "affordable", "value", "worth", "budget", "цена", "стоим", "дорог", "деш", "бюджет"),
        new("Landlord communication", "owner", "landlord", "host", "reply", "responsive", "communication", "helpful", "support", "rude", "хозя", "владел", "арендод", "общен", "ответ", "поддерж", "груб"),
        new("Noise", "noise", "noisy", "quiet", "loud", "street", "neighbor", "neighbour", "sound", "шум", "тих", "громк", "сосед"),
        new("Condition and repairs", "repair", "renovat", "maintenance", "broken", "leak", "old", "condition", "fix", "ремонт", "состоян", "сломан", "теч", "стар", "почин"),
        new("Amenities", "furniture", "kitchen", "internet", "wifi", "heating", "bathroom", "appliance", "aircon", "conditioner", "laundry", "мебел", "кухн", "интернет", "вайф", "отоп", "ванн", "техник", "кондицион", "стирал"),
        new("Transport and parking", "transport", "commute", "bus", "station", "parking", "road", "access", "транспорт", "автобус", "станц", "парков", "дорог"),
        new("Safety", "safe", "safety", "secure", "security", "danger", "unsafe", "безопас", "охран", "опас"),
        new("Space", "spacious", "small", "room", "layout", "size", "cramped", "space", "простор", "тесн", "комнат", "площад")
    };

    private static readonly string[] PositiveSignals =
    {
        "clean", "cozy", "cosy", "quiet", "spacious", "convenient", "good", "great", "excellent", "perfect",
        "comfortable", "nice", "friendly", "recommend", "чист", "уют", "тих", "простор", "удоб", "отлич",
        "хорош", "комфорт", "рекомен"
    };

    private static readonly string[] NegativeSignals =
    {
        "dirty", "noisy", "expensive", "rude", "broken", "small", "bad", "terrible", "awful", "problem",
        "issue", "leak", "smell", "slow", "гряз", "шум", "дорог", "груб", "сломан", "тесн", "плох",
        "ужас", "проблем", "теч", "запах"
    };

    public ReviewSummaryViewModel? BuildSummary(IEnumerable<ReviewApartment> reviews)
    {
        var preparedReviews = reviews?
            .Select(PrepareReview)
            .Where(review => review.HasSignal)
            .ToList() ?? new List<PreparedReview>();

        if (preparedReviews.Count < MinimumReviewsForSummary)
        {
            return null;
        }

        var ratedReviews = preparedReviews
            .Where(review => review.Rating.HasValue)
            .ToList();

        double? averageRating = ratedReviews.Count > 0
            ? ratedReviews.Average(review => review.Rating!.Value)
            : null;

        var positiveReviews = preparedReviews
            .Where(review => GetSentiment(review) > 0)
            .ToList();

        var negativeReviews = preparedReviews
            .Where(review => GetSentiment(review) < 0)
            .ToList();

        int neutralCount = preparedReviews.Count - positiveReviews.Count - negativeReviews.Count;

        return new ReviewSummaryViewModel
        {
            Overview = BuildOverview(preparedReviews.Count, averageRating, positiveReviews.Count, negativeReviews.Count, neutralCount),
            RecentTrend = BuildRecentTrend(preparedReviews, averageRating),
            RatingBreakdown = BuildRatingBreakdown(ratedReviews),
            PositiveHighlights = SelectHighlights(CountThemes(positiveReviews), positiveReviews.Count),
            NegativeHighlights = SelectHighlights(CountThemes(negativeReviews), negativeReviews.Count)
        };
    }

    private static PreparedReview PrepareReview(ReviewApartment review)
    {
        var comment = (review.Comment ?? string.Empty).Trim();
        var tokens = Tokenize(comment);

        return new PreparedReview(review.Rating, review.CreatedAt, tokens);
    }

    private static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        return Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}-]+")
            .Select(match => match.Value)
            .ToList();
    }

    private static int GetSentiment(PreparedReview review)
    {
        if (review.Rating >= 4)
        {
            return 1;
        }

        if (review.Rating <= 2)
        {
            return -1;
        }

        int signalScore = CountSignals(review.Tokens, PositiveSignals) - CountSignals(review.Tokens, NegativeSignals);

        if (review.Rating == 3)
        {
            return signalScore switch
            {
                > 0 => 1,
                < 0 => -1,
                _ => 0
            };
        }

        return signalScore switch
        {
            >= 2 => 1,
            <= -2 => -1,
            _ => 0
        };
    }

    private static int CountSignals(IEnumerable<string> tokens, IEnumerable<string> signals)
    {
        int count = 0;

        foreach (var token in tokens)
        {
            if (signals.Any(signal => token.Contains(signal, StringComparison.Ordinal)))
            {
                count++;
            }
        }

        return count;
    }

    private static Dictionary<string, int> CountThemes(IEnumerable<PreparedReview> reviews)
    {
        var themeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var review in reviews)
        {
            var themesInReview = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var theme in ThemeDefinitions)
            {
                if (review.Tokens.Any(token => theme.Keywords.Any(keyword => token.Contains(keyword, StringComparison.Ordinal))))
                {
                    themesInReview.Add(theme.Label);
                }
            }

            foreach (var themeLabel in themesInReview)
            {
                if (!themeCounts.TryAdd(themeLabel, 1))
                {
                    themeCounts[themeLabel]++;
                }
            }
        }

        return themeCounts;
    }

    private static List<string> SelectHighlights(Dictionary<string, int> themeCounts, int themedReviewCount)
    {
        if (themeCounts.Count == 0 || themedReviewCount == 0)
        {
            return new List<string>();
        }

        return themeCounts
            .Where(item => item.Value >= 2 || (double)item.Value / themedReviewCount >= 0.4)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key)
            .Take(3)
            .Select(item => item.Key)
            .ToList();
    }

    private static string BuildOverview(int totalReviews, double? averageRating, int positiveCount, int negativeCount, int neutralCount)
    {
        string tone = DetermineTone(averageRating, positiveCount, negativeCount, totalReviews);
        string reviewLabel = totalReviews == 1 ? "review" : "reviews";

        if (averageRating.HasValue)
        {
            return $"Based on {totalReviews} {reviewLabel}, feedback is {tone} with an average rating of {averageRating.Value:F1}/5.";
        }

        return $"Based on {totalReviews} {reviewLabel}, feedback is {tone}. Positive mentions: {positiveCount}, mixed or neutral: {neutralCount}, critical: {negativeCount}.";
    }

    private static string DetermineTone(double? averageRating, int positiveCount, int negativeCount, int totalReviews)
    {
        double positiveShare = totalReviews > 0 ? (double)positiveCount / totalReviews : 0;
        double negativeShare = totalReviews > 0 ? (double)negativeCount / totalReviews : 0;

        if (averageRating.HasValue)
        {
            if (averageRating.Value >= 4.3 && negativeShare <= 0.2)
            {
                return "mostly positive";
            }

            if (averageRating.Value >= 3.6 && positiveShare >= negativeShare)
            {
                return "mixed, but leaning positive";
            }

            if (averageRating.Value >= 2.8)
            {
                return "mixed";
            }

            return "mostly critical";
        }

        if (positiveShare >= 0.55 && negativeShare <= 0.2)
        {
            return "mostly positive";
        }

        if (negativeShare >= 0.45)
        {
            return "mostly critical";
        }

        return "mixed";
    }

    private static string BuildRecentTrend(IEnumerable<PreparedReview> reviews, double? overallAverageRating)
    {
        if (!overallAverageRating.HasValue)
        {
            return string.Empty;
        }

        var recentReviews = reviews
            .Where(review => review.CreatedAt.HasValue && review.Rating.HasValue)
            .OrderByDescending(review => review.CreatedAt)
            .Take(RecentReviewWindow)
            .ToList();

        if (recentReviews.Count < RecentReviewWindow)
        {
            return string.Empty;
        }

        double recentAverage = recentReviews.Average(review => review.Rating!.Value);

        if (recentAverage >= overallAverageRating.Value + 0.4)
        {
            return "Recent reviews are stronger than the overall average.";
        }

        if (recentAverage <= overallAverageRating.Value - 0.4)
        {
            return "Recent reviews are more mixed than the overall average.";
        }

        return "Recent reviews are consistent with the overall average.";
    }

    private static string BuildRatingBreakdown(IEnumerable<PreparedReview> ratedReviews)
    {
        var breakdown = Enumerable.Range(1, 5)
            .OrderByDescending(star => star)
            .Select(star => new
            {
                Star = star,
                Count = ratedReviews.Count(review => review.Rating == star)
            })
            .Where(item => item.Count > 0)
            .Select(item => $"{item.Star}★ {item.Count}")
            .ToList();

        return breakdown.Count == 0
            ? string.Empty
            : $"Rating split: {string.Join(" • ", breakdown)}";
    }

    private sealed record PreparedReview(int? Rating, DateTime? CreatedAt, List<string> Tokens)
    {
        public bool HasSignal => Rating.HasValue || Tokens.Count > 0;
    }

    private sealed record ThemeDefinition(string Label, params string[] Keywords);
}

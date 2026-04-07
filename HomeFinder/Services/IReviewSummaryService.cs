using HomeFinder.Models;

namespace HomeFinder.Services;

public interface IReviewSummaryService
{
    ReviewSummaryViewModel? BuildSummary(IEnumerable<ReviewApartment> reviews);
}

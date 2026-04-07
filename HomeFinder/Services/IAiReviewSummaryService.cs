using HomeFinder.Models;

namespace HomeFinder.Services;

public interface IAiReviewSummaryService
{
    Task<ReviewSummaryFetchResult> GetSummaryAsync(
        int apartmentId,
        IReadOnlyCollection<ReviewApartment> reviews,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}


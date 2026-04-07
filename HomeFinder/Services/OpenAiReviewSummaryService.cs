using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HomeFinder.Models;

namespace HomeFinder.Services;

public class OpenAiReviewSummaryService : IAiReviewSummaryService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OpenAiReviewSummaryService> _logger;
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> GenerationLocks = new();

    private static SemaphoreSlim LockForApartment(int apartmentId) =>
        GenerationLocks.GetOrAdd(apartmentId, _ => new SemaphoreSlim(1, 1));

    public OpenAiReviewSummaryService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<OpenAiReviewSummaryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ReviewSummaryFetchResult> GetSummaryAsync(
        int apartmentId,
        IReadOnlyCollection<ReviewApartment> reviews,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var sourceReviews = reviews
            .Where(r => !string.IsNullOrWhiteSpace(r.Comment) || r.Rating.HasValue)
            .OrderByDescending(r => r.CreatedAt ?? DateTime.MinValue)
            .ToList();

        if (sourceReviews.Count == 0)
        {
            return new ReviewSummaryFetchResult
            {
                Status = "no_data",
                Message = "No reviews for summary."
            };
        }

        var cached = await ReadCacheAsync(apartmentId, cancellationToken);
        bool hasFreshCache = cached?.Summary != null && cached.GeneratedAtUtc >= DateTime.UtcNow.Subtract(CacheLifetime);

        if (!forceRefresh && hasFreshCache)
        {
            return new ReviewSummaryFetchResult
            {
                Status = "ready",
                Message = "Can be updated by pressing the button below.",
                GeneratedAtUtc = cached!.GeneratedAtUtc,
                Summary = cached.Summary
            };
        }

        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ReviewSummaryFetchResult
            {
                Status = cached?.Summary != null ? "ready" : "disabled",
                Message = cached?.Summary != null
                    ? "OpenAI key not configured; showing cached summary."
                    : "Summary unavailable: OpenAI key is not set.",
                GeneratedAtUtc = cached?.GeneratedAtUtc,
                Summary = cached?.Summary,
                Diagnostic = RedactForClient("Конфигурация: пустой OpenAI:ApiKey (appsettings / переменные среды).")
            };
        }

        // Один запрос к OpenAI на квартиру за раз; внутри — await одного HTTP-вызова (без фонового polling со стороны клиента)
        var gate = LockForApartment(apartmentId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            cached = await ReadCacheAsync(apartmentId, cancellationToken);
            hasFreshCache = cached?.Summary != null && cached.GeneratedAtUtc >= DateTime.UtcNow.Subtract(CacheLifetime);
            if (!forceRefresh && hasFreshCache)
            {
                return new ReviewSummaryFetchResult
                {
                    Status = "ready",
                    Message = "Can be updated by pressing the button below.",
                    GeneratedAtUtc = cached!.GeneratedAtUtc,
                    Summary = cached.Summary
                };
            }

            ReviewSummaryViewModel? summary;
            string? generateDiagnostic = null;
            try
            {
                (summary, generateDiagnostic) = await GenerateSummaryAsync(sourceReviews, apiKey, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate OpenAI review summary for apartment {ApartmentId}", apartmentId);
                summary = null;
                generateDiagnostic = $"Exception: {TruncateDiagnostic(ex.Message, 400)}";
            }

            if (summary != null)
            {
                var generatedAt = DateTime.UtcNow;
                await WriteCacheAsync(apartmentId, new ReviewSummaryCacheEntry
                {
                    GeneratedAtUtc = generatedAt,
                    Summary = summary
                }, cancellationToken);

                return new ReviewSummaryFetchResult
                {
                    Status = "ready",
                    Message = forceRefresh ? "Summary updated." : "Generated.",
                    GeneratedAtUtc = generatedAt,
                    Summary = summary
                };
            }

            if (cached?.Summary != null)
            {
                return new ReviewSummaryFetchResult
                {
                    Status = "ready",
                    Message = "Try to update summary later",
                    GeneratedAtUtc = cached.GeneratedAtUtc,
                    Summary = cached.Summary,
                    Diagnostic = RedactForClient(string.IsNullOrWhiteSpace(generateDiagnostic)
                        ? "OpenAI did not return a valid summary (see server logs)."
                        : generateDiagnostic)
                };
            }

            return new ReviewSummaryFetchResult
            {
                Status = "disabled",
                Message = "Try to update summary later",
                GeneratedAtUtc = null,
                Summary = null,
                Diagnostic = RedactForClient(string.IsNullOrWhiteSpace(generateDiagnostic)
                    ? "Reason unknown (see server logs: OpenAiReviewSummaryService)."
                    : generateDiagnostic)
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(ReviewSummaryViewModel? Summary, string? Diagnostic)> GenerateSummaryAsync(
        IReadOnlyCollection<ReviewApartment> reviews,
        string apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(3);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var model = _configuration["OpenAI:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-4o-mini";
            }

            var reviewText = string.Join("\n\n", reviews.Select((review, index) =>
            {
                var date = review.CreatedAt?.ToString("dd.MM.yyyy") ?? "unknown date";
                var rating = review.Rating.HasValue ? $"{review.Rating.Value}/5" : "no rating";
                var comment = (review.Comment ?? string.Empty).Trim();
                return $"{index + 1}. Date: {date}; Rating: {rating}; Review: {comment}";
            }));

            var payload = new
            {
                model,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content =
                            "You summarize apartment reviews. Use only the provided reviews. Return valid JSON with keys: " +
                            "overview (string), recentTrend (string), ratingBreakdown (string), positiveHighlights (array of strings), negativeHighlights (array of strings). " +
                            "All text values must be in English. Keep it concise, factual, and do not hallucinate."
                    },
                    new
                    {
                        role = "user",
                        content =
                            "Summarize the apartment reviews below.\n" +
                            "Rules:\n" +
                            "- overview: 2-4 short sentences\n" +
                            "- recentTrend: one short sentence or empty string\n" +
                            "- ratingBreakdown: compact text if ratings are available, otherwise empty string\n" +
                            "- positiveHighlights: up to 3 short phrases\n" +
                            "- negativeHighlights: up to 3 short phrases\n\n" +
                            $"Reviews:\n{reviewText}"
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var response = await client.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI summary request failed: {StatusCode} {Body}", (int)response.StatusCode, raw);
                return (null, $"OpenAI HTTP {(int)response.StatusCode}: {TruncateDiagnostic(raw)}");
            }

            using var outerDoc = JsonDocument.Parse(raw);
            var rootEl = outerDoc.RootElement;
            if (!rootEl.TryGetProperty("choices", out var choicesEl) ||
                choicesEl.ValueKind != JsonValueKind.Array ||
                choicesEl.GetArrayLength() == 0)
            {
                return (null, $"OpenAI response has no choices[]. Fragment: {TruncateDiagnostic(raw)}");
            }

            var choice0 = choicesEl[0];
            if (!choice0.TryGetProperty("message", out var messageEl))
            {
                return (null, $"No message in first choice. Fragment: {TruncateDiagnostic(raw)}");
            }

            if (!messageEl.TryGetProperty("content", out var contentEl) ||
                contentEl.ValueKind != JsonValueKind.String)
            {
                return (null, "Field message.content is missing or not a string.");
            }

            var content = contentEl.GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return (null, "Empty message.content from OpenAI.");
            }

            using var summaryDoc = JsonDocument.Parse(content);
            var root = summaryDoc.RootElement;

            var vm = new ReviewSummaryViewModel
            {
                Overview = GetString(root, "overview"),
                RecentTrend = GetString(root, "recentTrend"),
                RatingBreakdown = GetString(root, "ratingBreakdown"),
                PositiveHighlights = GetStringArray(root, "positiveHighlights"),
                NegativeHighlights = GetStringArray(root, "negativeHighlights")
            };

            return (vm, null);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, "Timeout or connection error to OpenAI (check network and client timeout). " + ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return (null, $"HTTP error to OpenAI: {TruncateDiagnostic(ex.Message)}");
        }
        catch (JsonException ex)
        {
            return (null, $"JSON parsing error (response or model content): {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GenerateSummaryAsync");
            return (null, $"Error: {TruncateDiagnostic(ex.Message)}");
        }
    }

    private static string TruncateDiagnostic(string? text, int max = 500)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(empty)";
        }

        var t = text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }

    /// <summary>Убирает из diagnostic для браузера фрагменты ключей из сообщений OpenAI (401 invalid_api_key и т.д.).</summary>
    private static string? RedactForClient(string? diagnostic)
    {
        if (string.IsNullOrEmpty(diagnostic))
        {
            return diagnostic;
        }

        return Regex.Replace(
            diagnostic,
            @"sk-[a-zA-Z0-9_*-]{8,}",
            "[ключ скрыт]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private async Task<ReviewSummaryCacheEntry?> ReadCacheAsync(int apartmentId, CancellationToken cancellationToken)
    {
        var path = GetCachePath(apartmentId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ReviewSummaryCacheEntry>(stream, JsonOptions, cancellationToken);
    }

    private async Task WriteCacheAsync(int apartmentId, ReviewSummaryCacheEntry entry, CancellationToken cancellationToken)
    {
        var finalPath = GetCachePath(apartmentId);
        var directory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, entry, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }

    private string GetCachePath(int apartmentId)
    {
        return Path.Combine(_environment.ContentRootPath, "App_Data", "review-summary-cache", $"apartment-{apartmentId}.json");
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static List<string> GetStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }

    private sealed class ReviewSummaryCacheEntry
    {
        public DateTime GeneratedAtUtc { get; set; }

        public ReviewSummaryViewModel? Summary { get; set; }
    }
}


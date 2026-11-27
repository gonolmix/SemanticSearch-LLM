using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SemanticSearch.Application.ExternalSources
{
    public sealed class SemanticScholarClient : IScholarlySourceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly ILogger<SemanticScholarClient> _logger;
        private static readonly SemaphoreSlim _throttle = new(1, 1);
        private static DateTime _nextAllowedTimeUtc = DateTime.MinValue;
        public string SourceName => "semanticscholar";

        public SemanticScholarClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<SemanticScholarClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("semanticscholar");
            _apiKey = configuration["ScholarlySources:SemanticScholar:ApiKey"];
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            }
        }

        public async Task<IReadOnlyList<ScholarlyWork>> SearchAsync(
            string query,
            int take = 25,
            CancellationToken cancellationToken = default)
        {
            var safeTake = Math.Clamp(take, 1, 100);

            var url = $"graph/v1/paper/search" +
                      $"?query={Uri.EscapeDataString(query)}" +
                      $"&limit={safeTake}" +
                      "&fields=title,abstract,year,externalIds,openAccessPdf,url,authors,citationCount";

            const int maxAttempts = 4;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                await WaitRateLimitAsync(cancellationToken);

                try
                {
                    using var response = await _httpClient.SendAsync(
                        new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);

                    // 429 = исчерпан лимит: ждём Retry-After или 2^n секунд и повторяем
                    if ((int)response.StatusCode == 429 && attempt < maxAttempts)
                    {
                        var backoff = response.Headers.RetryAfter?.Delta
                                      ?? TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2); // 4s, 8s, 16s
                        _logger.LogWarning(
                            "Semantic Scholar 429 (attempt {Attempt}/{Max}). Retry in {Seconds}s",
                            attempt, maxAttempts, backoff.TotalSeconds);
                        await Task.Delay(backoff, cancellationToken);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var scholarResponse = JsonSerializer.Deserialize<ScholarResponse>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (scholarResponse?.Data is null)
                    {
                        _logger.LogWarning("No results from Semantic Scholar");
                        return Array.Empty<ScholarlyWork>();
                    }

                    var works = scholarResponse.Data
                        .Where(paper => paper is not null && !string.IsNullOrWhiteSpace(paper.Abstract))
                        .Select(paper => new ScholarlyWork
                        {
                            Source = SourceName,
                            ExternalId = paper.ExternalIds?.Doi
                                         ?? paper.ExternalIds?.Arxiv
                                         ?? paper.PaperId
                                         ?? $"semanticscholar_{Guid.NewGuid()}",
                            Title = paper.Title ?? "(no title)",
                            Abstract = paper.Abstract,
                            FullText = null,
                            Authors = paper.Authors?
                                .Select(a => a.Name ?? "")
                                .Where(name => !string.IsNullOrWhiteSpace(name))
                                .ToList() ?? new List<string>(),
                            PublicationDate = paper.Year.HasValue
                                ? new DateTime(paper.Year.Value, 1, 1)
                                : null,
                            SourceUrl = paper.OpenAccessPdf?.Url ?? paper.Url,
                            CitationCount = paper.CitationCount,
                            MetadataJson = null
                        })
                        .ToList();

                    _logger.LogInformation("Found {Count} works from Semantic Scholar", works.Count);
                    return works;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
                    _logger.LogWarning(ex,
                        "Semantic Scholar request failed (attempt {Attempt}/{Max}). Retry in {Seconds}s",
                        attempt, maxAttempts, backoff.TotalSeconds);
                    await Task.Delay(backoff, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching Semantic Scholar");
                    return Array.Empty<ScholarlyWork>();
                }
            }

            _logger.LogWarning("Semantic Scholar: all {Max} attempts exhausted for query '{Query}'", maxAttempts, query);
            return Array.Empty<ScholarlyWork>();
        }
        private static async Task WaitRateLimitAsync(CancellationToken ct)
        {
            await _throttle.WaitAsync(ct);
            try
            {
                var now = DateTime.UtcNow;
                if (now < _nextAllowedTimeUtc)
                    await Task.Delay(_nextAllowedTimeUtc - now, ct);
                _nextAllowedTimeUtc = DateTime.UtcNow + TimeSpan.FromSeconds(1.1);
            }
            finally
            {
                _throttle.Release();
            }
        }
    }

    // DTO для ответа Semantic Scholar
    internal sealed class ScholarResponse
    {
        public int Total { get; set; }
        public int Offset { get; set; }
        public int Next { get; set; }
        public List<ScholarPaper>? Data { get; set; }
    }

    internal sealed class ScholarPaper
    {
        public string? PaperId { get; set; }
        public string? Title { get; set; }
        public string? Abstract { get; set; }
        public int? Year { get; set; }
        public string? Url { get; set; }
        public ExternalIds? ExternalIds { get; set; }
        public OpenAccessPdf? OpenAccessPdf { get; set; }
        public List<ScholarAuthor>? Authors { get; set; }
        public int CitationCount { get; set; }
    }

    internal sealed class ExternalIds
    {
        public string? Doi { get; set; }
        public string? Arxiv { get; set; }
        public string? PubMed { get; set; }
    }

    internal sealed class OpenAccessPdf
    {
        public string? Url { get; set; }
    }

    internal sealed class ScholarAuthor
    {
        public string? Name { get; set; }
    }
}
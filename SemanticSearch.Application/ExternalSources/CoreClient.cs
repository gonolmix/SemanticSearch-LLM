using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticSearch.Application.ExternalSources
{
    public sealed class CoreClient : IScholarlySourceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly ILogger<CoreClient> _logger;

        public string SourceName => "core";

        public CoreClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<CoreClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("core");
            _apiKey = configuration["ScholarlySources:Core:ApiKey"];
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }
        }

        public async Task<IReadOnlyList<ScholarlyWork>> SearchAsync(
            string query,
            int take = 25,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("CORE API key not configured");
                return Array.Empty<ScholarlyWork>();
            }

            var safeTake = Math.Clamp(take, 1, 100);

            var url = $"v3/search/works/?q={Uri.EscapeDataString(query)}&limit={safeTake}";

            _logger.LogInformation("Searching CORE: {Query}", query);

            try
            {
                var response = await _httpClient.GetStringAsync(url, cancellationToken);
                var coreResponse = JsonSerializer.Deserialize<CoreResponse>(
                    response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (coreResponse?.Hits is null)
                {
                    _logger.LogWarning("No results from CORE");
                    return Array.Empty<ScholarlyWork>();
                }

                var works = coreResponse.Hits
                    .Where(h => !string.IsNullOrWhiteSpace(h.Abstract))
                    .Select(h => new ScholarlyWork
                    {
                        Source = SourceName,
                        ExternalId = h.Doi ?? h.CoreId?.ToString() ?? $"core_{Guid.NewGuid()}",
                        Title = h.Title ?? "(no title)",
                        Abstract = h.Abstract,
                        FullText = h.FullText, // CORE часто отдаёт полные тексты
                        Authors = h.Authors?
                            .Select(a => a.Name ?? "")
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .ToList() ?? new List<string>(),
                        PublicationDate = h.PublishedDate.HasValue ? h.PublishedDate.Value : null,
                        SourceUrl = h.DownloadUrl ?? h.SourceFulltextUrls?.FirstOrDefault(),
                        CitationCount = null, // CORE не отдаёт цитирования в базовом API
                        MetadataJson = null
                    })
                    .ToList();

                _logger.LogInformation("Found {Count} works from CORE", works.Count);
                return works;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching CORE");
                return Array.Empty<ScholarlyWork>();
            }
        }
    }

    // DTO для ответа CORE
    internal sealed class CoreResponse
    {
        [JsonPropertyName("hits")] public List<CoreWork>? Hits { get; set; }
        [JsonPropertyName("totalHits")] public int TotalHits { get; set; }
    }

    internal sealed class CoreWork
    {
        [JsonPropertyName("id")] public int? CoreId { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("abstract")] public string? Abstract { get; set; }
        [JsonPropertyName("doi")] public string? Doi { get; set; }
        [JsonPropertyName("authors")] public List<CoreAuthor>? Authors { get; set; }
        [JsonPropertyName("publishedDate")] public DateTime? PublishedDate { get; set; }
        [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; set; }
        [JsonPropertyName("sourceFulltextUrls")] public List<string>? SourceFulltextUrls { get; set; }
        [JsonPropertyName("fullText")] public string? FullText { get; set; }
    }

    internal sealed class CoreAuthor
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }
}
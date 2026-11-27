using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticSearch.Application.ExternalSources
{
    public sealed class OpenAlexClient : IScholarlySourceClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _mailTo;
        private readonly ILogger<OpenAlexClient> _logger;

        public string SourceName => "openalex";

        public OpenAlexClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<OpenAlexClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("openalex");
            _mailTo = configuration["ScholarlySources:OpenAlex:MailTo"]
                      ?? "dev@semanticsearch.local";
            _logger = logger;

            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                $"SemanticSearch-LLM/1.0 (mailto:{_mailTo})");
        }

        public async Task<IReadOnlyList<ScholarlyWork>> SearchAsync(
            string query,
            int take = 25,
            CancellationToken cancellationToken = default)
        {
            var safeTake = Math.Clamp(take, 1, 100);

            var url = $"works?search={Uri.EscapeDataString(query)}" +
                      "&filter=has_abstract:true" +
                      $"&per-page={safeTake}" +
                      "&select=id,doi,display_name,publication_date,cited_by_count," +
                      "abstract_inverted_index,open_access,primary_location,authorships" +
                      $"&mailto={Uri.EscapeDataString(_mailTo)}";

            _logger.LogInformation("Searching OpenAlex: {Query}", query);

            try
            {
                var response = await _httpClient.GetStringAsync(url, cancellationToken);
                var openAlexResponse = JsonSerializer.Deserialize<OpenAlexResponse>(
                    response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (openAlexResponse?.Results is null)
                {
                    _logger.LogWarning("No results from OpenAlex");
                    return Array.Empty<ScholarlyWork>();
                }

                var works = openAlexResponse.Results
                    .Where(work => work is not null && work.AbstractInvertedIndex is not null)
                    .Select(work => new ScholarlyWork
                    {
                        Source = SourceName,
                        ExternalId = work.Doi ?? work.Id ?? $"openalex_{Guid.NewGuid()}",
                        Title = work.DisplayName ?? "(no title)",
                        Abstract = OpenAlexTextBuilder.Reconstruct(work.AbstractInvertedIndex),
                        FullText = null,
                        Authors = work.Authorships?
                            .Select(a => a.Author?.DisplayName ?? "")
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .ToList() ?? new List<string>(),
                        PublicationDate = DateTime.TryParse(work.PublicationDate, out var date)
                            ? date
                            : null,
                        SourceUrl = work.OpenAccess?.OaUrl
                                    ?? work.PrimaryLocation?.LandingPageUrl
                                    ?? work.Id,
                        CitationCount = work.CitedByCount,
                        MetadataJson = null
                    })
                    .Where(work => !string.IsNullOrWhiteSpace(work.Abstract))
                    .ToList();

                _logger.LogInformation("Found {Count} works from OpenAlex", works.Count);
                return works;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching OpenAlex");
                return Array.Empty<ScholarlyWork>();
            }
        }
    }

    // DTO для ответа OpenAlex
    internal sealed class OpenAlexResponse
    {
        [JsonPropertyName("results")] public List<OpenAlexWork>? Results { get; set; }
    }

    internal sealed class OpenAlexWork
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("doi")] public string? Doi { get; set; }
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
        [JsonPropertyName("publication_date")] public string? PublicationDate { get; set; }
        [JsonPropertyName("cited_by_count")] public int CitedByCount { get; set; }
        [JsonPropertyName("abstract_inverted_index")] public Dictionary<string, int[]>? AbstractInvertedIndex { get; set; }
        [JsonPropertyName("open_access")] public OpenAlexOa? OpenAccess { get; set; }
        [JsonPropertyName("primary_location")] public OpenAlexLocation? PrimaryLocation { get; set; }
        [JsonPropertyName("authorships")] public List<OpenAlexAuthorship>? Authorships { get; set; }
    }

    internal sealed class OpenAlexOa
    {
        [JsonPropertyName("oa_status")] public string? OaStatus { get; set; }
        [JsonPropertyName("oa_url")] public string? OaUrl { get; set; }
    }

    internal sealed class OpenAlexLocation
    {
        [JsonPropertyName("landing_page_url")] public string? LandingPageUrl { get; set; }
    }

    internal sealed class OpenAlexAuthorship
    {
        [JsonPropertyName("author")] public OpenAlexAuthor? Author { get; set; }
    }

    internal sealed class OpenAlexAuthor
    {
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    }
}
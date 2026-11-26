using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SemanticSearch.Application.ExternalSources
{
    public sealed class EuropePmcClient : IScholarlySourceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EuropePmcClient> _logger;

        public string SourceName => "europepmc";

        public EuropePmcClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<EuropePmcClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("europepmc");
            _logger = logger;
        }

        public async Task<IReadOnlyList<ScholarlyWork>> SearchAsync(
            string query,
            int take = 25,
            CancellationToken cancellationToken = default)
        {
            var safeTake = Math.Clamp(take, 1, 100);

            var url = $"search?query={Uri.EscapeDataString(query)}" +
                      $"&format=json&pageSize={safeTake}&resultType=core";

            _logger.LogInformation("Searching Europe PMC: {Query}", query);

            try
            {
                var response = await _httpClient.GetStringAsync(url, cancellationToken);
                var pmcResponse = JsonSerializer.Deserialize<EuropePmcResponse>(
                    response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (pmcResponse?.ResultList?.Result is null)
                {
                    _logger.LogWarning("No results from Europe PMC");
                    return Array.Empty<ScholarlyWork>();
                }

                var works = pmcResponse.ResultList.Result
                    .Where(r => !string.IsNullOrWhiteSpace(r.AbstractText))
                    .Select(r => new ScholarlyWork
                    {
                        Source = SourceName,
                        ExternalId = r.Doi ?? r.Pmid ?? r.Pmcid ?? $"europepmc_{Guid.NewGuid()}",
                        Title = r.Title ?? "(no title)",
                        Abstract = r.AbstractText,
                        FullText = null, // Можно получить через fullTextXML для OA-статей
                        Authors = r.AuthorList?.Author?
                            .Select(a => a.FullName ?? "")
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .ToList() ?? new List<string>(),
                        PublicationDate = DateTime.TryParse(r.PubYear, out var date) ? date : null,
                        SourceUrl = r.Doi != null ? $"https://doi.org/{r.Doi}" : null,
                        CitationCount = r.CitedByCount,
                        MetadataJson = null
                    })
                    .ToList();

                _logger.LogInformation("Found {Count} works from Europe PMC", works.Count);
                return works;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Europe PMC");
                return Array.Empty<ScholarlyWork>();
            }
        }
    }

    // DTO для ответа Europe PMC
    internal sealed class EuropePmcResponse
    {
        [JsonPropertyName("resultList")] public EuropePmcResultList? ResultList { get; set; }
    }

    internal sealed class EuropePmcResultList
    {
        [JsonPropertyName("result")] public List<EuropePmcResult>? Result { get; set; }
    }

    internal sealed class EuropePmcResult
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("abstractText")] public string? AbstractText { get; set; }
        [JsonPropertyName("doi")] public string? Doi { get; set; }
        [JsonPropertyName("pmid")] public string? Pmid { get; set; }
        [JsonPropertyName("pmcid")] public string? Pmcid { get; set; }
        [JsonPropertyName("pubYear")] public string? PubYear { get; set; }
        [JsonPropertyName("citedByCount")] public int? CitedByCount { get; set; }
        [JsonPropertyName("authorList")] public EuropePmcAuthorList? AuthorList { get; set; }
    }

    internal sealed class EuropePmcAuthorList
    {
        [JsonPropertyName("author")] public List<EuropePmcAuthor>? Author { get; set; }
    }

    internal sealed class EuropePmcAuthor
    {
        [JsonPropertyName("fullName")] public string? FullName { get; set; }
    }
}
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Xml.Linq;

namespace SemanticSearch.Application.ExternalSources
{
    public sealed class ArxivClient : IScholarlySourceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ArxivClient> _logger;
        private static readonly SemaphoreSlim _throttle = new(1, 1);
        private static DateTime _nextAllowedTimeUtc = DateTime.MinValue;

        public string SourceName => "arxiv";

        public ArxivClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ArxivClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient("arxiv");
            _logger = logger;

            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent", "SemanticSearch-LLM/1.0 (mailto:dev@semanticsearch.local)");
        }

        public async Task<IReadOnlyList<ScholarlyWork>> SearchAsync(
            string query,
            int take = 25,
            CancellationToken cancellationToken = default)
        {
            var safeTake = Math.Clamp(take, 1, 50); // arXiv рекомендует ≤ 50

            // Формируем URL для Atom API
            var url = $"api/query?search_query=all:{Uri.EscapeDataString(query)}" +
                      $"&start=0&max_results={safeTake}";

            _logger.LogInformation("Searching arXiv: {Query}", query);

            await WaitRateLimitAsync(cancellationToken);

            try
            {
                var response = await _httpClient.GetStringAsync(url, cancellationToken);
                return ParseAtomFeed(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching arXiv");
                return Array.Empty<ScholarlyWork>();
            }
        }

        private List<ScholarlyWork> ParseAtomFeed(string xml)
        {
            var works = new List<ScholarlyWork>();

            try
            {
                var doc = XDocument.Parse(xml);
                var ns = XNamespace.Get("http://www.w3.org/2005/Atom");

                var entries = doc.Descendants(ns + "entry");

                foreach (var entry in entries)
                {
                    var title = entry.Element(ns + "title")?.Value?.Trim() ?? "(no title)";
                    var summary = entry.Element(ns + "summary")?.Value?.Trim();
                    var published = entry.Element(ns + "published")?.Value;
                    var id = entry.Element(ns + "id")?.Value;

                    var authors = entry.Elements(ns + "author")
                        .Select(a => a.Element(ns + "name")?.Value ?? "")
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToList();

                    var links = entry.Elements(ns + "link");
                    var pdfUrl = links.FirstOrDefault(l => l.Attribute("title")?.Value == "pdf")?.Attribute("href")?.Value;
                    var absUrl = links.FirstOrDefault(l => l.Attribute("type")?.Value == "text/html")?.Attribute("href")?.Value;

                    if (string.IsNullOrWhiteSpace(summary))
                        continue;

                    works.Add(new ScholarlyWork
                    {
                        Source = SourceName,
                        ExternalId = id ?? $"arxiv_{Guid.NewGuid()}",
                        Title = title,
                        Abstract = summary,
                        FullText = null, // Полные тексты только PDF, парсить отдельно
                        Authors = authors,
                        PublicationDate = DateTime.TryParse(published, out var date) ? date : null,
                        SourceUrl = absUrl ?? pdfUrl ?? id,
                        CitationCount = null, // arXiv не отдаёт цитирования
                        MetadataJson = null
                    });
                }

                _logger.LogInformation("Found {Count} works from arXiv", works.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse arXiv Atom feed");
            }

            return works;
        }

        private static async Task WaitRateLimitAsync(CancellationToken ct)
        {
            await _throttle.WaitAsync(ct);
            try
            {
                var now = DateTime.UtcNow;
                if (now < _nextAllowedTimeUtc)
                    await Task.Delay(_nextAllowedTimeUtc - now, ct);
                _nextAllowedTimeUtc = DateTime.UtcNow + TimeSpan.FromSeconds(3); // arXiv: 3 сек пауза
            }
            finally
            {
                _throttle.Release();
            }
        }
    }
}
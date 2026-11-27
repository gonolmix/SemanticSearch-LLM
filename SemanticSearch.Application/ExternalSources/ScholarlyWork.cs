using System.Text.Json;

namespace SemanticSearch.Application.ExternalSources
{
    public sealed record ScholarlyWork
    {
        public required string Source { get; init; }
        public required string ExternalId { get; init; }
        public required string Title { get; init; }
        public string? Abstract { get; init; }
        public string? FullText { get; init; }
        public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
        public DateTime? PublicationDate { get; init; }
        public string? SourceUrl { get; init; }
        public int? CitationCount { get; init; }
        public string? MetadataJson { get; init; }

        public string BuildMetadata()
        {
            var metadata = new
            {
                source = Source,
                external_id = ExternalId,
                nid = ExternalIdNormalizer.Normalize(ExternalId),   // ← единый ключ для анти-дубликата
                authors = Authors,
                publication_date = PublicationDate?.ToString("yyyy-MM-dd"),
                citation_count = CitationCount
            };
            return JsonSerializer.Serialize(metadata);
        }
    }
}
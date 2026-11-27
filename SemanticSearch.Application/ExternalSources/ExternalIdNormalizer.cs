using System;

namespace SemanticSearch.Application.ExternalSources
{
    public static class ExternalIdNormalizer
    {
        public static string Normalize(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            id = id.Trim();

            if (id.StartsWith("https://doi.org/", StringComparison.OrdinalIgnoreCase))
                id = id["https://doi.org/".Length..];
            else if (id.StartsWith("http://doi.org/", StringComparison.OrdinalIgnoreCase))
                id = id["http://doi.org/".Length..];
            else if (id.StartsWith("doi:", StringComparison.OrdinalIgnoreCase))
                id = id["doi:".Length..];
            else if (id.StartsWith("https://openalex.org/", StringComparison.OrdinalIgnoreCase))
                id = "openalex:" + id["https://openalex.org/".Length..];
            else if (id.StartsWith("https://arxiv.org/abs/", StringComparison.OrdinalIgnoreCase))
                id = "arxiv:" + id["https://arxiv.org/abs/".Length..];
            else if (id.StartsWith("http://arxiv.org/abs/", StringComparison.OrdinalIgnoreCase))   // ← arXiv Atom отдаёт http
                id = "arxiv:" + id["http://arxiv.org/abs/".Length..];
            else if (id.StartsWith("https://arxiv.org/pdf/", StringComparison.OrdinalIgnoreCase))
                id = "arxiv:" + id["https://arxiv.org/pdf/".Length..];
            else if (id.StartsWith("http://arxiv.org/pdf/", StringComparison.OrdinalIgnoreCase))
                id = "arxiv:" + id["http://arxiv.org/pdf/".Length..];
            else if (id.StartsWith("https://www.ncbi.nlm.nih.gov/pmc/articles/PMC", StringComparison.OrdinalIgnoreCase))
                id = "pmc:" + id["https://www.ncbi.nlm.nih.gov/pmc/articles/PMC".Length..];
            else if (id.StartsWith("PMC", StringComparison.OrdinalIgnoreCase))
                id = "pmc:" + id["PMC".Length..];

            id = id.ToLowerInvariant().Trim();

            if (id.StartsWith("arxiv:", StringComparison.OrdinalIgnoreCase))
            {
                var vIdx = id.LastIndexOf('v');
                if (vIdx > 6 && int.TryParse(id[(vIdx + 1)..], out _))
                    id = id[..vIdx];
            }

            return id;
        }

        /// Часть id без префикса источника — для сопоставления с URL-формой в старых метаданных
        public static string RawTail(string normalized)
        {
            var idx = normalized.IndexOf(':');
            return idx >= 0 ? normalized[(idx + 1)..] : normalized;
        }
    }
}
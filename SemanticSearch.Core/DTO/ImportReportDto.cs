namespace SemanticSearch.Core.DTO
{
    public sealed class ImportReportDto
    {
        public string Query { get; set; } = string.Empty;
        public List<string> Sources { get; set; } = new();
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }

        public TimeSpan? Duration => CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt
            : null;

        public int TotalProcessed => Imported + Skipped;
    }
}
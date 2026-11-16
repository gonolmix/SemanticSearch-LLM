using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SemanticSearch.Application.ExternalSources;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Core.DTO;
using SemanticSearch.Infrastructure.Data;

namespace SemanticSearch.Application.Services
{
    public sealed class ScholarlyImportService
    {
        private readonly IEnumerable<IScholarlySourceClient> _clients;
        private readonly IDocumentService _documentService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<ScholarlyImportService> _logger;

        public ScholarlyImportService(
            IEnumerable<IScholarlySourceClient> clients,
            IDocumentService documentService,
            AppDbContext dbContext,
            ILogger<ScholarlyImportService> logger)
        {
            _clients = clients;
            _documentService = documentService;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<ImportReportDto> ImportAsync(
            string query,
            IReadOnlyCollection<string> sources,
            int takePerSource = 25,
            CancellationToken cancellationToken = default)
        {
            var report = new ImportReportDto
            {
                Query = query,
                Sources = sources.ToList(),
                StartedAt = DateTime.UtcNow
            };

            var selectedClients = _clients
                .Where(c => sources.Contains(c.SourceName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (!selectedClients.Any())
            {
                report.ErrorMessage = "No valid sources selected";
                _logger.LogWarning("Import: no valid sources selected");
                return report;
            }

            foreach (var client in selectedClients)
            {
                _logger.LogInformation("Importing from {Source}", client.SourceName);

                IReadOnlyList<ScholarlyWork> works;
                try
                {
                    works = await client.SearchAsync(query, takePerSource, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to search {Source}", client.SourceName);
                    report.Errors.Add($"{client.SourceName}: {ex.Message}");
                    continue;
                }

                foreach (var work in works)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var result = await ImportSingleWorkAsync(work, cancellationToken);

                        if (result.Success)
                        {
                            report.Imported++;
                            _logger.LogInformation(
                                "Imported [{Source}] {Title} (DocumentId: {Id})",
                                work.Source, work.Title, result.DocumentId);
                        }
                        else
                        {
                            report.Skipped++;
                            report.Errors.Add($"{work.Source} '{work.Title}': {result.ErrorMessage}");
                            // ← ГЛАВНОЕ НОВОЕ: причина пропуска теперь видна в консоли
                            _logger.LogWarning(
                                "SKIPPED [{Source}] '{Title}': {Reason}",
                                work.Source, work.Title, result.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        report.Skipped++;
                        report.Errors.Add($"{work.Source} '{work.Title}': EXCEPTION {ex.Message}");
                        _logger.LogError(ex, "FAILED to import [{Source}] '{Title}'", work.Source, work.Title);
                    }
                }
            }

            report.CompletedAt = DateTime.UtcNow;
            // ← Итоговая сводка в лог
            _logger.LogInformation(
                "Import report: query='{Query}' imported={Imported} skipped={Skipped} errors=[{Errors}]",
                query, report.Imported, report.Skipped, string.Join(" | ", report.Errors));

            return report;
        }

        private async Task<ImportResult> ImportSingleWorkAsync(
            ScholarlyWork work,
            CancellationToken cancellationToken)
        {
            var text = work.FullText ?? work.Abstract;

            if (string.IsNullOrWhiteSpace(text))
                return ImportResult.Fail("no abstract/fulltext in source record");

            if (await IsDuplicateAsync(work.ExternalId, cancellationToken))
                return ImportResult.Fail($"duplicate detected (externalId='{work.ExternalId}')");

            var documentDto = new DocumentDto
            {
                Title = Truncate(work.Title, 500),
                Description = Truncate(work.Abstract, 4000),
                SourceUrl = Truncate(work.SourceUrl, 1000),
                SourceType = "api"
            };

            var createdDoc = await _documentService.CreateDocumentAsync(documentDto);

            var entity = await _dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == createdDoc.Id, cancellationToken);
            if (entity != null)
            {
                entity.Metadata = work.BuildMetadata();
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await _documentService.ImportFromTextAsync(createdDoc.Id, text, maxParagraphLength: 1500);

            var paragraphs = await _documentService.GetParagraphsByDocumentAsync(createdDoc.Id);
            var paragraphIds = paragraphs.Select(p => p.Id).ToList();
            if (paragraphIds.Any())
            {
                await _documentService.BulkIndexParagraphsAsync(paragraphIds);
            }

            return ImportResult.Ok(createdDoc.Id);
        }

        private async Task<bool> IsDuplicateAsync(string externalId, CancellationToken cancellationToken)
        {
            var normalized = ExternalIdNormalizer.Normalize(externalId);
            if (normalized.Length < 8)
                return false;

            var tail = ExternalIdNormalizer.RawTail(normalized);
            if (tail.Length < 8)
                return false;

            return await _dbContext.Documents.AnyAsync(d => d.Metadata != null && (
                   d.Metadata.Contains($":\"{normalized}\"") ||   // новое поле nid или нормализованный external_id
                   d.Metadata.Contains($"\"{normalized}\"") ||   // id в кавычках как есть
                   d.Metadata.Contains($"/{tail}\"")),            // старая URL-форма: https://doi.org/... или https://openalex.org/...
                cancellationToken);
        }
        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private sealed record ImportResult
        {
            public bool Success { get; init; }
            public int DocumentId { get; init; }
            public string? ErrorMessage { get; init; }

            public static ImportResult Ok(int documentId) => new() { Success = true, DocumentId = documentId };
            public static ImportResult Fail(string error) => new() { Success = false, ErrorMessage = error };
        }
    }
}
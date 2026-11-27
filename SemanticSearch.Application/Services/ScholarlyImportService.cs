using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SemanticSearch.Application.ExternalSources;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Core.DTO;
using SemanticSearch.Infrastructure.Data;
using System.Text.Json;

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
                    _logger.LogInformation("Received {Count} works from {Source}", works.Count, client.SourceName);
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
                                "Imported [{Source}] '{Title}' (DocumentId: {Id})",
                                work.Source, work.Title, result.DocumentId);
                        }
                        else
                        {
                            report.Skipped++;
                            report.Errors.Add($"{work.Source} '{work.Title}': {result.ErrorMessage}");
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
            _logger.LogInformation(
                "Import report: query='{Query}' imported={Imported} skipped={Skipped} errors=[{Errors}]",
                query, report.Imported, report.Skipped, string.Join(" | ", report.Errors));

            return report;
        }

        private async Task<ImportResult> ImportSingleWorkAsync(
            ScholarlyWork work,
            CancellationToken cancellationToken)
        {
            // 1. Проверяем наличие текста
            var text = work.FullText ?? work.Abstract;
            if (string.IsNullOrWhiteSpace(text))
                return ImportResult.Fail("no abstract/fulltext in source record");

            // 2. Проверяем дубликат
            if (await IsDuplicateAsync(work.ExternalId, cancellationToken))
                return ImportResult.Fail($"duplicate detected (externalId='{work.ExternalId}')");

            // 3. Создаём документ с Metadata сразу
            var metadata = work.BuildMetadata();
            var documentDto = new DocumentDto
            {
                Title = Truncate(work.Title, 500),
                Description = Truncate(work.Abstract, 4000),
                SourceUrl = Truncate(work.SourceUrl, 1000),
                SourceType = "api",
                Metadata = metadata  // передаём Metadata сразу
            };

            DocumentDto createdDoc;
            try
            {
                createdDoc = await _documentService.CreateDocumentAsync(documentDto);
            }
            catch (Exception ex)
            {
                return ImportResult.Fail($"failed to create document: {ex.Message}");
            }

            // 4. Импортируем текст в параграфы
            try
            {
                await _documentService.ImportFromTextAsync(createdDoc.Id, text, maxParagraphLength: 1500);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import text for document {Id}", createdDoc.Id);
                // Откатываем создание документа
                await _documentService.DeleteDocumentAsync(createdDoc.Id);
                return ImportResult.Fail($"failed to import text: {ex.Message}");
            }

            // 5. Индексируем параграфы
            try
            {
                var paragraphs = await _documentService.GetParagraphsByDocumentAsync(createdDoc.Id);
                var paragraphIds = paragraphs.Select(p => p.Id).ToList();

                if (paragraphIds.Any())
                {
                    var indexedCount = await _documentService.BulkIndexParagraphsAsync(paragraphIds);
                    _logger.LogDebug("Indexed {Count} paragraphs for document {Id}", indexedCount, createdDoc.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index paragraphs for document {Id}", createdDoc.Id);
                // Документ и параграфы уже созданы, просто логируем ошибку индексации
                // Не откатываем, так как параграфы можно переиндексировать позже
            }

            return ImportResult.Ok(createdDoc.Id);
        }

        private async Task<bool> IsDuplicateAsync(string externalId, CancellationToken cancellationToken)
        {
            var normalized = ExternalIdNormalizer.Normalize(externalId);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 8)
                return false;

            var tail = ExternalIdNormalizer.RawTail(normalized);
            if (string.IsNullOrWhiteSpace(tail) || tail.Length < 8)
                return false;

            // Проверяем точное совпадение нормализованного ID в Metadata
            return await _dbContext.Documents.AnyAsync(d =>
                d.Metadata != null &&
                d.SourceType == "api" &&
                (
                    // Точное совпадение нормализованного ID
                    d.Metadata.Contains($"\"nid\":\"{normalized}\"") ||
                    // Совпадение по external_id
                    d.Metadata.Contains($"\"external_id\":\"{normalized}\"") ||
                    // Совпадение по tail (для URL-форм)
                    d.Metadata.Contains($"/{tail}\"")
                ),
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
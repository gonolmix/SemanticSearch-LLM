using Microsoft.Extensions.Logging;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Core.DTO;
using SemanticSearch.Core.Entities;
using SemanticSearch.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly DocumentRepository _docRepo;
        private readonly ParagraphRepository _paraRepo;
        private readonly ISemanticSearchService _searchService;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(
            DocumentRepository docRepo,
            ParagraphRepository paraRepo,
            ISemanticSearchService searchService,
            ILogger<DocumentService> logger)
        {
            _docRepo = docRepo;
            _paraRepo = paraRepo;
            _searchService = searchService;
            _logger = logger;
        }

        public async Task<DocumentDto> CreateDocumentAsync(DocumentDto dto)
        {
            var doc = new Document
            {
                Title = dto.Title,
                Description = dto.Description,
                SourceUrl = dto.SourceUrl,
                SourceType = dto.SourceType,
                Metadata = dto.Paragraphs.Any() ? $"{{\"initial_paragraphs\":{dto.Paragraphs.Count}}}" : null
            };

            var created = await _docRepo.AddAsync(doc);
            return MapToDto(created);
        }

        public async Task<DocumentDto?> GetDocumentAsync(int id)
        {
            var doc = await _docRepo.GetByIdWithParagraphsAsync(id);
            return doc == null ? null : MapToDto(doc);
        }

        public async Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync(bool onlyActive = true)
        {
            var docs = onlyActive
                ? await _docRepo.FindAsync(d => d.IsActive)
                : await _docRepo.GetAllAsync();

            return docs.Select(MapToDto);
        }

        public async Task<DocumentDto> UpdateDocumentAsync(int id, DocumentDto dto)
        {
            var doc = await _docRepo.GetByIdAsync(id);
            if (doc == null)
                throw new KeyNotFoundException($"Document {id} not found");

            doc.Title = dto.Title;
            doc.Description = dto.Description;
            doc.SourceUrl = dto.SourceUrl;
            doc.UpdatedAt = DateTime.UtcNow;

            _docRepo.Update(doc);
            return MapToDto(doc);
        }

        public async Task<bool> DeleteDocumentAsync(int id)
        {
            var doc = await _docRepo.GetByIdAsync(id);
            if (doc == null)
                return false;

            doc.IsActive = false;
            doc.UpdatedAt = DateTime.UtcNow;
            _docRepo.Update(doc);
            return true;
        }

        public async Task<ParagraphDto> AddParagraphAsync(int documentId, string content, int order)
        {
            var doc = await _docRepo.GetByIdAsync(documentId);
            if (doc == null)
                throw new KeyNotFoundException($"Document {documentId} not found");

            var para = new Paragraph
            {
                DocumentId = documentId,
                Content = content,
                ParagraphOrder = order
            };

            var created = await _paraRepo.AddAsync(para);
            return MapToDto(created);
        }

        public async Task<ParagraphDto?> GetParagraphAsync(int id)
        {
            var para = await _paraRepo.GetByIdAsync(id);
            return para == null ? null : MapToDto(para);
        }

        public async Task<IEnumerable<ParagraphDto>> GetParagraphsByDocumentAsync(int documentId)
        {
            var paras = await _paraRepo.FindAsync(p => p.DocumentId == documentId);
            return paras.OrderBy(p => p.ParagraphOrder).Select(MapToDto);
        }

        public async Task<bool> UpdateParagraphAsync(int id, string content)
        {
            var para = await _paraRepo.GetByIdAsync(id);
            if (para == null)
                return false;

            para.Content = content;
            para.IndexedAt = null; // Требует переиндексации
            _paraRepo.Update(para);
            return true;
        }

        public async Task<bool> DeleteParagraphAsync(int id)
        {
            var para = await _paraRepo.GetByIdAsync(id);
            if (para == null)
                return false;

            _paraRepo.Remove(para);
            return true;
        }

        public async Task<int> ImportFromTextAsync(int documentId, string fullText, int maxParagraphLength = 500)
        {
            var doc = await _docRepo.GetByIdAsync(documentId);
            if (doc == null)
                throw new KeyNotFoundException($"Document {documentId} not found");

            // Разбиение текста на абзацы
            var paragraphs = SplitIntoParagraphs(fullText, maxParagraphLength);

            var entities = paragraphs.Select((content, idx) => new Paragraph
            {
                DocumentId = documentId,
                Content = content,
                ParagraphOrder = idx + 1
            }).ToList();

            await _paraRepo.AddRangeAsync(entities);
            _logger.LogInformation($"Imported {entities.Count} paragraphs for document {documentId}");

            return entities.Count;
        }

        public async Task<int> BulkIndexParagraphsAsync(IEnumerable<int> paragraphIds)
        {
            var indexed = 0;

            foreach (var id in paragraphIds)
            {
                try
                {
                    var success = await _searchService.ReindexParagraphAsync(id);
                    if (success) indexed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to index paragraph {id}");
                }
            }

            return indexed;
        }

        public async Task<DocumentStatsDto> GetDocumentStatsAsync(int documentId)
        {
            var paragraphs = await _paraRepo.FindAsync(p => p.DocumentId == documentId);

            return new DocumentStatsDto
            {
                TotalParagraphs = paragraphs.Count(),
                IndexedParagraphs = paragraphs.Count(p => p.IndexedAt != null),
                TotalWords = paragraphs.Sum(p => p.WordCount),
                TotalChars = paragraphs.Sum(p => p.CharCount),
                LastIndexedAt = paragraphs.Max(p => p.IndexedAt)
            };
        }

        // Вспомогательные методы
        private DocumentDto MapToDto(Document doc)
        {
            return new DocumentDto
            {
                Id = doc.Id,
                Title = doc.Title,
                Description = doc.Description,
                SourceUrl = doc.SourceUrl,
                SourceType = doc.SourceType,
                CreatedAt = doc.CreatedAt,
                ParagraphCount = doc.Paragraphs?.Count ?? 0,
                IsIndexed = doc.Paragraphs?.All(p => p.IndexedAt != null) ?? false,
                Paragraphs = doc.Paragraphs?.Select(MapToDto).ToList() ?? new List<ParagraphDto>()
            };
        }

        private ParagraphDto MapToDto(Paragraph para)
        {
            return new ParagraphDto
            {
                Id = para.Id,
                DocumentId = para.DocumentId,
                Content = para.Content,
                ParagraphOrder = para.ParagraphOrder,
                WordCount = para.WordCount,
                IndexedAt = para.IndexedAt,
                HasVector = para.IndexedAt != null
            };
        }

        private List<string> SplitIntoParagraphs(string text, int maxLength)
        {
            var paragraphs = new List<string>();

            // Разбиваем по двойным переносам строки
            var raw = Regex.Split(text, @"\n\s*\n");

            foreach (var block in raw)
            {
                var clean = block.Trim();
                if (string.IsNullOrWhiteSpace(clean))
                    continue;

                // Если блок слишком длинный - разбиваем по предложениям
                if (clean.Length > maxLength)
                {
                    var sentences = Regex.Split(clean, @"[.!?]+\s*");
                    var current = "";

                    foreach (var sentence in sentences)
                    {
                        if (string.IsNullOrWhiteSpace(sentence))
                            continue;

                        if ((current + sentence).Length <= maxLength)
                        {
                            current += sentence + ". ";
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(current))
                                paragraphs.Add(current.Trim());
                            current = sentence + ". ";
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(current))
                        paragraphs.Add(current.Trim());
                }
                else
                {
                    paragraphs.Add(clean);
                }
            }

            return paragraphs;
        }
    }
}
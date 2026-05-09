using SemanticSearch.Core.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Interfaces
{
    public interface IDocumentService
    {
        // CRUD для документов
        Task<DocumentDto> CreateDocumentAsync(DocumentDto dto);
        Task<DocumentDto?> GetDocumentAsync(int id);
        Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync(bool onlyActive = true);
        Task<DocumentDto> UpdateDocumentAsync(int id, DocumentDto dto);
        Task<bool> DeleteDocumentAsync(int id);

        // CRUD для абзацев
        Task<ParagraphDto> AddParagraphAsync(int documentId, string content, int order);
        Task<ParagraphDto?> GetParagraphAsync(int id);
        Task<IEnumerable<ParagraphDto>> GetParagraphsByDocumentAsync(int documentId);
        Task<bool> UpdateParagraphAsync(int id, string content);
        Task<bool> DeleteParagraphAsync(int id);

        // Массовые операции
        Task<int> ImportFromTextAsync(int documentId, string fullText, int maxParagraphLength = 500);
        Task<int> BulkIndexParagraphsAsync(IEnumerable<int> paragraphIds);

        // Статистика
        Task<DocumentStatsDto> GetDocumentStatsAsync(int documentId);
    }
}

using SemanticSearch.Core.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Interfaces
{
    public interface ISemanticSearchService
    {
        /// <summary>
        /// Семантический поиск с выбором алгоритма
        /// </summary>
        Task<SearchResponseDto> SearchAsync(SearchRequestDto request);

        /// <summary>
        /// Индексировать все неиндексированные абзацы
        /// </summary>
        Task<int> IndexPendingParagraphsAsync();

        /// <summary>
        /// Переиндексировать конкретный абзац
        /// </summary>
        Task<bool> ReindexParagraphAsync(int paragraphId);

        /// <summary>
        /// Получить статистику индексации
        /// </summary>
        Task<IndexingStatsDto> GetIndexingStatsAsync();
    }
}

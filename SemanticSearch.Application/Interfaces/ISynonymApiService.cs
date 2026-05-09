using SemanticSearch.Infrastructure.AdditionalClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Interfaces
{
    public interface ISynonymApiService
    {
        /// <summary>
        /// Получить синонимы из внешнего API
        /// </summary>
        Task<List<SynonymResult>> GetSynonymsAsync(string word, int maxResults = 10);

        /// <summary>
        /// Получить синонимы с кэшированием
        /// </summary>
        Task<List<SynonymResult>> GetSynonymsWithCacheAsync(string word, int maxResults = 10);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Enums
{
    public enum SearchAlgorithm
    {
        /// <summary>
        /// Только TF-IDF (быстро, без семантики)
        /// </summary>
        Tfidf = 0,

        /// <summary>
        /// Только BM25 (быстро, лучше TF-IDF)
        /// </summary>
        BM25 = 1,

        /// <summary>
        /// Только векторный поиск (семантика, медленнее)
        /// </summary>
        Vector = 2,

        /// <summary>
        /// Гибридный: TF-IDF + BM25 + Vector (лучшая точность)
        /// </summary>
        Hybrid = 3,

        /// <summary>
        /// Гибридный с упором на векторы (для семантических запросов)
        /// </summary>
        HybridSemantic = 4,

        /// <summary>
        /// Гибридный с упором на ключевые слова (для точных запросов)
        /// </summary>
        HybridKeyword = 5
    }
}

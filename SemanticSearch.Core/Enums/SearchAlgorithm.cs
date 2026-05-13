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
        /// Гибридный: 80% вектор + 20% ключевые слова
        /// </summary>
        Hybrid = 0,

        /// <summary>
        /// Чисто векторный: 100% семантика
        /// </summary>
        Vector = 1
    }
}

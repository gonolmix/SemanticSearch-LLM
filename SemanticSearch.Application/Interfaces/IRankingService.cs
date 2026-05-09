using SemanticSearch.Core.Entities;
using SemanticSearch.Infrastructure.AdditionalClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Interfaces
{
    public interface IRankingService
    {
        /// <summary>
        /// Рассчитать релевантность с использованием всех методов
        /// </summary>
        Task<List<ParagraphScore>> RankAsync(
            string query,
            List<Paragraph> paragraphs,
            float[] queryVector,
            Core.Enums.SearchAlgorithm algorithm);

        /// <summary>
        /// Только BM25 скоринг
        /// </summary>
        Dictionary<int, double> CalculateBM25Scores(string query, List<Paragraph> paragraphs);

        /// <summary>
        /// Только TF-IDF скоринг
        /// </summary>
        Dictionary<int, double> CalculateTfidfScores(string query, List<Paragraph> paragraphs);

        /// <summary>
        /// Векторный скоринг (косинусное сходство)
        /// </summary>
        Dictionary<int, double> CalculateVectorScores(float[] queryVector, List<Paragraph> paragraphs);
    }
}

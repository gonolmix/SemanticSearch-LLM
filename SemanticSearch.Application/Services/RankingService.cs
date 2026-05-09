using Microsoft.Extensions.Logging;
using SemanticSearch.Application.Helpers;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Core.Entities;
using SemanticSearch.Core.Enums;
using SemanticSearch.Infrastructure.AdditionalClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Services
{
    public class RankingService : IRankingService
    {
        private readonly ILinguisticService _linguistic;
        private readonly ILogger<RankingService> _logger;

        public RankingService(ILinguisticService linguistic, ILogger<RankingService> logger)
        {
            _linguistic = linguistic;
            _logger = logger;
        }

        public void InitializeStats(IEnumerable<Paragraph> paragraphs)
        {
            // Пустая заглушка - пока не используем TF-IDF
        }

        public async Task<List<ParagraphScore>> RankAsync(
            string query,
            List<Paragraph> paragraphs,
            float[] queryVector,
            SearchAlgorithm algorithm)
        {
            _logger.LogError($"🔍 RANKING START: {paragraphs.Count} paragraphs, query vector dim={queryVector?.Length}");

            var results = new List<ParagraphScore>();

            // 🔥 ПРОСТОЙ векторный поиск БЕЗ усложнений
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Embedding == null || paragraph.Embedding.Length == 0)
                {
                    _logger.LogWarning($"⚠️ Paragraph {paragraph.Id} has NO embedding!");
                    continue;
                }

                // 🔥 Косинусное сходство
                var similarity = VectorMath.CosineSimilarity(queryVector, paragraph.Embedding);
                var score = (similarity + 1) / 2; // [-1, 1] → [0, 1]

                _logger.LogError($"📊 Para {paragraph.Id}: similarity={similarity:F4}, score={score:F4}");

                var paraScore = new ParagraphScore
                {
                    Paragraph = paragraph,
                    VectorScore = score,
                    TfidfScore = 0,
                    BM25Score = 0,
                    TotalScore = score // 🔥 Просто векторный скор, без бонусов!
                };

                results.Add(paraScore);
            }

            // 🔥 Сортировка
            var sorted = results.OrderByDescending(r => r.TotalScore).ToList();

            _logger.LogError($"🏆 TOP 3:");
            for (int i = 0; i < Math.Min(3, sorted.Count); i++)
            {
                _logger.LogError($"  #{i + 1}: Para {sorted[i].Paragraph.Id} - {sorted[i].TotalScore * 100:F2}%");
            }

            _logger.LogError($"🔍 RANKING END");

            return sorted;
        }

        public Dictionary<int, double> CalculateTfidfScores(string query, List<Paragraph> paragraphs)
        {
            return new Dictionary<int, double>();
        }

        public Dictionary<int, double> CalculateBM25Scores(string query, List<Paragraph> paragraphs)
        {
            return new Dictionary<int, double>();
        }

        public Dictionary<int, double> CalculateVectorScores(float[] queryVector, List<Paragraph> paragraphs)
        {
            return new Dictionary<int, double>();
        }
    }
}
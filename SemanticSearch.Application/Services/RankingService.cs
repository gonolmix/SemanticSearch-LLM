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
        }

        public async Task<List<ParagraphScore>> RankAsync(string query, List<Paragraph> paragraphs, float[] queryVector, SearchAlgorithm algorithm)
        {
            _logger.LogInformation($"Ranking on Thread {Thread.CurrentThread.ManagedThreadId}, ManagedThreadId={Thread.CurrentThread.ManagedThreadId}");
            var results = new List<ParagraphScore>();

            // Веса для режимов
            double vectorWeight = algorithm == SearchAlgorithm.Hybrid ? 0.80 : 1.0;
            double keywordWeight = algorithm == SearchAlgorithm.Hybrid ? 0.20 : 0.0;

            // Подсчёт бонуса за ключевые слова (только для Hybrid)
            var keywordBonus = algorithm == SearchAlgorithm.Hybrid
                ? CalculateKeywordBonus(query, paragraphs)
                : new Dictionary<int, double>();

            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Embedding == null || paragraph.Embedding.Length == 0)
                    continue;

                // Векторный скор (0.0 - 1.0)
                var similarity = VectorMath.CosineSimilarity(queryVector, paragraph.Embedding);
                var vectorScore = (similarity + 1) / 2;

                // Ключевые слова (0.0 - 1.0)
                var keywordScore = keywordBonus.GetValueOrDefault(paragraph.Id, 0);

                // Итоговый скор
                var totalScore = (vectorScore * vectorWeight) + (keywordScore * keywordWeight);
                totalScore = Math.Min(totalScore, 1.0); // Кап на 100%

                results.Add(new ParagraphScore
                {
                    Paragraph = paragraph,
                    VectorScore = vectorScore,
                    TfidfScore = keywordScore,
                    BM25Score = 0,
                    TotalScore = totalScore
                });
            }

            return results.OrderByDescending(r => r.TotalScore).ToList();
        }

        // простой бонус за ключевые слова
        private Dictionary<int, double> CalculateKeywordBonus(string query, List<Paragraph> paragraphs)
        {
            var bonuses = new Dictionary<int, double>();
            var queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLower().Trim(new[] { '.', ',', '!', '?', ';', ':', ' ', '\t', '\n' }))
                .Where(w => w.Length > 3)
                .ToList();

            foreach (var p in paragraphs)
            {
                var content = p.Content.ToLower();
                var title = p.Document?.Title?.ToLower() ?? "";

                double bonus = 0;
                foreach (var word in queryWords)
                {
                    if (title.Contains(word)) bonus += 0.5;
                    else if (content.Contains(word)) bonus += 0.15;
                }

                bonuses[p.Id] = Math.Min(bonus, 1.0);
            }

            return bonuses;
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
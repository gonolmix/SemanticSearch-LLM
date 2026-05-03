using SemanticSearch.Application.IServices;
using SemanticSearch.Core.Classes;
using SemanticSearch.Core.DTO;
using SemanticSearch.Core.Models;
using SemanticSearch.Infrastructure.Repositories;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Services
{
    public class AdvancedSearchService : ISearchService
    {
        private readonly LinguisticRepository _repo;
        private readonly ILinguisticService _linguistic;
        private List<Paragraph> _allParagraphs;
        private Dictionary<int, ParagraphStats> _paragraphStats;
        private Dictionary<string, int> _documentFrequency;
        private int _totalDocuments;

        public AdvancedSearchService(LinguisticRepository repo, ILinguisticService linguistic)
        {
            _repo = repo;
            _linguistic = linguistic;
        }

        public async Task InitializeCacheAsync()
        {
            _allParagraphs = await _repo.GetAllParagraphsAsync();
            _totalDocuments = _allParagraphs.Count;
            _paragraphStats = new Dictionary<int, ParagraphStats>();
            _documentFrequency = new Dictionary<string, int>();

            // Предварительная обработка всех абзацев
            Parallel.ForEach(_allParagraphs, paragraph =>
            {
                var tokens = _linguistic.Tokenize(paragraph.Content)
                    .Select(t => _linguistic.NormalizeWord(t))
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                paragraph.ProcessedTokens = tokens;

                // Статистика для TF-IDF
                var termFrequency = tokens.GroupBy(t => t)
                    .ToDictionary(g => g.Key, g => g.Count());

                _paragraphStats[paragraph.Id] = new ParagraphStats
                {
                    TermFrequency = termFrequency,
                    TotalTerms = tokens.Count
                };

                lock (_documentFrequency)
                {
                    foreach (var term in termFrequency.Keys)
                    {
                        if (_documentFrequency.ContainsKey(term))
                            _documentFrequency[term]++;
                        else
                            _documentFrequency[term] = 1;
                    }
                }
            });
        }

        public async Task<SearchResultDto> SearchAsync(string query)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new SearchResultDto { Query = query };

            if (_allParagraphs == null || !_allParagraphs.Any())
            {
                await InitializeCacheAsync();
            }

            // Обработка запроса
            var queryTokens = _linguistic.Tokenize(query);
            var normalizedQueryTokens = queryTokens
                .Select(t => _linguistic.NormalizeWord(t))
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            var expandedQueryTokens = _linguistic.ExpandQuery(queryTokens);

            if (!expandedQueryTokens.Any())
                return result;

            // IDF для слов запроса
            var queryIdf = new Dictionary<string, double>();
            foreach (var token in expandedQueryTokens.Distinct())
            {
                int docFreq = _documentFrequency.TryGetValue(token, out var freq) ? freq : 0;
                queryIdf[token] = Math.Log((double)_totalDocuments / (docFreq + 1)) + 1;
            }

            // Параллельный поиск с TF-IDF
            var concurrentResults = new ConcurrentBag<SearchMatchDto>();

            Parallel.ForEach(_allParagraphs, paragraph =>
            {
                if (paragraph.ProcessedTokens == null || !_paragraphStats.ContainsKey(paragraph.Id))
                    return;

                var stats = _paragraphStats[paragraph.Id];
                double totalScore = 0;
                var matchedWords = new List<string>();

                foreach (var qToken in expandedQueryTokens.Distinct())
                {
                    if (stats.TermFrequency.TryGetValue(qToken, out var tf))
                    {
                        // TF-IDF score
                        double termScore = (1 + Math.Log10(tf)) * queryIdf[qToken];
                        totalScore += termScore;
                        matchedWords.Add(qToken);
                    }
                }

                if (totalScore > 0)
                {
                    double normalizedScore = totalScore / Math.Sqrt(stats.TotalTerms);

                    if (ContainsExactPhrase(paragraph.Content, queryTokens))
                        normalizedScore *= 1.5;

                    var highlights = queryTokens
                        .Where(t => expandedQueryTokens.Contains(_linguistic.NormalizeWord(t)))
                        .ToList();

                    concurrentResults.Add(new SearchMatchDto
                    {
                        DocumentTitle = paragraph.Document?.Title ?? "Unknown",
                        ParagraphContent = paragraph.Content,
                        RelevanceScore = normalizedScore,
                        HighlightedWords = highlights
                    });
                }
            });

            result.Matches = concurrentResults
                .OrderByDescending(m => m.RelevanceScore)
                .Take(10)
                .ToList();

            stopwatch.Stop();
            result.TotalTimeMs = (int)stopwatch.ElapsedMilliseconds;

            return result;
        }

        private bool ContainsExactPhrase(string text, List<string> queryTokens)
        {
            var textLower = text.ToLower();
            foreach (var token in queryTokens.Where(t => t.Length > 3))
            {
                if (!textLower.Contains(token.ToLower()))
                    return false;
            }
            return true;
        }
    }

}

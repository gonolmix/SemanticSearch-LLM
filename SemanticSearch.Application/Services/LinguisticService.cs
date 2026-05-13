using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Infrastructure;
using SemanticSearch.Infrastructure.Repositories;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SemanticSearch.Application.AdditionalClasses
{
    public class LinguisticService : ILinguisticService
    {
        private readonly LinguisticRepository _repo;
        private readonly ISynonymApiService _synonymApi;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LinguisticService> _logger;

        private HashSet<string> _stopWords = new();
        private Dictionary<string, List<string>> _localSynonyms = new();
        private readonly ConcurrentDictionary<string, string> _lemmaCache = new();

        private static readonly string[] _russianSuffixes = new[]
        {
            "ами", "ями", "ого", "его", "ому", "ему", "ыми", "ими",
            "ой", "ей", "ым", "им", "ую", "юю", "ое", "ее", "ых", "их",
            "ть", "ти", "чь", "ешь", "ёшь", "ет", "ёт", "ем", "ём",
            "ете", "ёте", "ут", "ют", "ат", "ят", "л", "ла", "ло", "ли",
            "ться", "лся", "лась", "лось", "ый", "ий", "ом", "ем",
            "ах", "ях", "а", "я", "о", "е", "у", "ю", "ы", "и", "ь"
        };

        public LinguisticService(
            LinguisticRepository repo,
            ISynonymApiService synonymApi,
            IMemoryCache cache,
            ILogger<LinguisticService> logger)
        {
            _repo = repo;
            _synonymApi = synonymApi;
            _cache = cache;
            _logger = logger;
        }

        public async Task LoadDataAsync()
        {
            var stops = await _repo.GetStopWordsAsync();
            _stopWords = new HashSet<string>(stops.Select(s => s.Word.ToLower()));
            _logger.LogInformation($"Loaded {_stopWords.Count} stop words");

            var syns = await _repo.GetSynonymsAsync();
            _localSynonyms = syns
                .Where(s => s.IsActive)
                .GroupBy(s => s.SourceWord.ToLower())
                .ToDictionary(g => g.Key, g => g.Select(x => x.TargetWord.ToLower()).ToList());
            _logger.LogInformation($"Loaded {_localSynonyms.Count} local synonym groups");
        }

        public List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            // Извлекаются слова (кириллица, латиница, цифры)
            var matches = Regex.Matches(text.ToLower(), @"[a-zA-Zа-яА-Я0-9]+");

            // фильтр стоп-слов
            return matches
                .Select(m => m.Value)
                .Where(t => !IsStopWord(t))
                .ToList();
        }

        public bool IsStopWord(string word)
        {
            return _stopWords.Contains(word.ToLower());
        }

        public string NormalizeWord(string word)
        {
            var lower = word.ToLower();
            if (IsStopWord(lower))
                return string.Empty;

            if (lower.Length < 3)
                return lower;

            return _lemmaCache.GetOrAdd(lower, key =>
            {
                var nounSuffixes = new[]
                {
                    "ами", "ями", "ого", "его", "ому", "ему",
                    "ыми", "ими", "ах", "ях",
                    "ой", "ей", "ым", "им",
                    "ую", "юю", "ое", "ее", "ых", "их"
                };

                foreach (var suffix in nounSuffixes.OrderByDescending(s => s.Length))
                {
                    if (key.EndsWith(suffix) && key.Length > suffix.Length + 3)
                        return key.Substring(0, key.Length - suffix.Length);
                }

                // Окончания глаголов
                var verbSuffixes = new[]
                {
                    "ться", "тся", "ешь", "ёшь", "ет", "ёт",
                    "ем", "ём", "ете", "ёте", "ут", "ют", "ат", "ят",
                    "ла", "ло", "ли", "л"
                };

                foreach (var suffix in verbSuffixes.OrderByDescending(s => s.Length))
                {
                    if (key.EndsWith(suffix) && key.Length > suffix.Length + 3)
                    {
                        var stem = key.Substring(0, key.Length - suffix.Length);
                        return stem + "ть";
                    }
                }

                return key;
            });
        }

        public async Task<List<string>> ExpandQueryAsync(List<string> tokens)
        {
            var expanded = new HashSet<string>();

            foreach (var token in tokens)
            {
                var normalized = NormalizeWord(token);
                if (!string.IsNullOrEmpty(normalized))
                {
                    expanded.Add(normalized);

                    // Локальные синонимы из БД
                    if (_localSynonyms.TryGetValue(normalized, out var localSyns))
                    {
                        foreach (var syn in localSyns)
                            expanded.Add(syn);
                    }

                    // API синонимы (только для значимых слов)
                    if (token.Length > 3 && normalized.Length > 3)
                    {
                        var apiSyns = await _synonymApi.GetSynonymsWithCacheAsync(normalized, 5);
                        foreach (var syn in apiSyns)
                            expanded.Add(syn.Word.ToLower());
                    }

                    if (token.Length > 3)
                        expanded.Add(token.ToLower());
                }
            }

            _logger.LogDebug($"Expanded {tokens.Count} tokens to {expanded.Count} with synonyms");
            return expanded.ToList();
        }

        public List<string> GetSignificantTokens(List<string> tokens)
        {
            return tokens
                .Where(t => t.Length > 3 && !IsStopWord(t))
                .Select(NormalizeWord)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();
        }

        public void ClearCache()
        {
            _lemmaCache.Clear();
            _logger.LogInformation("Lemma cache cleared");
        }
    }
}
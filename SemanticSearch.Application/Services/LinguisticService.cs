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

        // Snowball стеммер для русского (упрощённая версия)
        // В продакшене используйте библиотеку Snowball.Stemmers
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

            // Извлекаем слова (кириллица, латиница, цифры)
            var matches = Regex.Matches(text.ToLower(), @"[a-zA-Zа-яА-Я0-9]+");

            // Фильтруем стоп-слова сразу
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
                // 🔥 Специальные правила для частых слов
                if (key.EndsWith("цию")) return key.Substring(0, key.Length - 3) + "ция";
                if (key.EndsWith("зации")) return key;
                if (key.EndsWith("примеры")) return "пример";
                if (key.EndsWith("алгоритмы")) return "алгоритм";
                if (key.EndsWith("сети")) return "сеть";
                if (key.EndsWith("модели")) return "модель";
                if (key.EndsWith("нейроны")) return "нейрон";
                if (key.EndsWith("веса")) return "вес";
                if (key.EndsWith("данные")) return "данные";
                if (key.EndsWith("обучения")) return "обучение";
                if (key.EndsWith("токены")) return "токен";
                if (key.EndsWith("слова")) return "слово";
                if (key.EndsWith("предложения")) return "предложение";

                // 🔥 Стандартные окончания существительных
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

                // 🔥 Окончания глаголов
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
                        // Добавляем инфинитивное окончание
                        return stem + "ть";
                    }
                }

                // Возвращаем как есть
                return key;
            });
        }
        private string LemmatizeSafe(string word)
            {
                if (word.Length < 4)
                    return word;

                if (word.EndsWith("ция") || word.EndsWith("тие") || word.EndsWith("ние"))
                    return word;

                var suffixes = new[]
                {
                    "ами", "ями", "ого", "его", "ому", "ему",
                    "ыми", "ими", "ой", "ей", "ым", "им",
                    "ую", "юю", "ое", "ее", "ых", "их",
                    "ах", "ях", "а", "я", "о", "е", "у", "ю", "ы", "и"
                };

            foreach (var suffix in suffixes.OrderByDescending(s => s.Length))
            {
                if (word.EndsWith(suffix) && word.Length > suffix.Length + 3)
                {
                    var stem = word.Substring(0, word.Length - suffix.Length);
                    if (stem.Length >= 3)
                        return stem;
                }
            }

            return word;
        }

        // Упрощённый стемминг для русского языка
        private string StemRussian(string word)
        {
            if (word.Length < 4)
                return word;

            // Пробуем отрезать известные суффиксы
            foreach (var suffix in _russianSuffixes.OrderByDescending(s => s.Length))
            {
                if (word.EndsWith(suffix) && word.Length > suffix.Length + 2)
                {
                    var stem = word.Substring(0, word.Length - suffix.Length);

                    // Минимальная длина корня
                    if (stem.Length >= 3)
                        return stem;
                }
            }

            return word;
        }

        private bool IsValidLemma(string original, string lemma)
        {
            // Слишком короткий результат
            if (lemma.Length < 3)
                return false;

            // Слишком большая разница в длине
            if (Math.Abs(lemma.Length - original.Length) > 5)
                return false;

            // Подозрительные окончания
            if (lemma.EndsWith("ть") && !original.EndsWith("ть") &&
                !lemma.EndsWith("ать") && !lemma.EndsWith("ять") && !lemma.EndsWith("ить"))
                return false;

            return true;
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

                    // Оригинал как фоллбэк
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
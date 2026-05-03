using SemanticSearch.Application.IServices;
using SemanticSearch.Infrastructure.Repositories;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Services
{
    public class AdvancedLinguisticService : ILinguisticService
    {
        private readonly LinguisticRepository _repo;
        private HashSet<string> _stopWords = new();
        private Dictionary<string, List<string>> _synonyms = new();
        private ConcurrentDictionary<string, string> _lemmaCache = new();

        // Правила лемматизации для существительных
        private readonly Dictionary<string, string[]> _nounEndings = new()
        {
            ["а"] = new[] { "ы", "е", "у", "ой", "ей", "ою", "ею", "ах", "ами" },
            ["я"] = new[] { "и", "е", "ю", "ей", "ею", "ях", "ями" },
            ["о"] = new[] { "а", "у", "ом", "ою", "е" },
            ["е"] = new[] { "я", "ю", "ем", "ею", "и", "ах", "ами" },
            ["ия"] = new[] { "ии", "ию", "ией", "иею", "иях", "иями" },
            ["ие"] = new[] { "ия", "ию", "ием", "иею", "иях", "иями" }
        };

        // Правила для глаголов
        private readonly Dictionary<string, string[]> _verbEndings = new()
        {
            ["ть"] = new[] { "ет", "ёт", "ут", "ют", "ат", "ят", "ешь", "ёшь", "ём", "ём", "ете", "ёте", "ем", "ёте" },
            ["ить"] = new[] { "ит", "им", "ите", "ишь", "ит", "им", "ите", "ят", "ат" },
            ["ать"] = new[] { "ает", "ают", "аешь", "аем", "аете", "аю", "яю" },
            ["овать"] = new[] { "ует", "уют", "уешь", "уем", "уете" },
            ["евать"] = new[] { "ует", "уют", "уешь", "уем", "уете" }
        };

        public AdvancedLinguisticService(LinguisticRepository repo)
        {
            _repo = repo;
        }

        public async Task LoadDataAsync()
        {
            var stops = await _repo.GetStopWordsAsync();
            _stopWords = new HashSet<string>(stops.Select(s => s.Word.ToLower()));

            var syns = await _repo.GetSynonymsAsync();
            _synonyms = syns.GroupBy(s => s.SourceWord.ToLower())
                            .ToDictionary(g => g.Key, g => g.Select(x => x.TargetWord.ToLower()).ToList());
        }

        public List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            var matches = Regex.Matches(text.ToLower(), @"[a-zA-Zа-яА-Я0-9]+");
            return matches.Select(m => m.Value).ToList();
        }

        public bool IsStopWord(string word)
        {
            return _stopWords.Contains(word.ToLower());
        }

        public string NormalizeWord(string word)
        {
            var lower = word.ToLower();
            if (IsStopWord(lower)) return string.Empty;

            return _lemmaCache.GetOrAdd(lower, key =>
            {
                var lemma = Lemmatize(key);
                return lemma;
            });
        }

        private string Lemmatize(string word)
        {
            if (word.Length < 3) return word;

            foreach (var kvp in _nounEndings)
            {
                if (word.EndsWith(kvp.Key))
                    return word;

                foreach (var ending in kvp.Value)
                {
                    if (word.EndsWith(ending))
                    {
                        var stem = word.Substring(0, word.Length - ending.Length);
                        return stem + kvp.Key;
                    }
                }
            }


            foreach (var kvp in _verbEndings)
            {
                if (word.EndsWith(kvp.Key))
                    return word; 

                foreach (var ending in kvp.Value)
                {
                    if (word.EndsWith(ending))
                    {
                        var stem = word.Substring(0, word.Length - ending.Length);
                        if (kvp.Key.EndsWith("овать") || kvp.Key.EndsWith("евать"))
                            return stem + "ова" + "ть";
                        return stem + kvp.Key;
                    }
                }
            }

            return StemSimple(word);
        }

        private string StemSimple(string word)
        {
            var endings = new[]
            {
                "ами", "ями", "ого", "его", "ому", "ему", "ому", "ему",
                "ыми", "ими", "ой", "ей", "ой", "ей", "ым", "им",
                "ую", "юю", "ое", "ее", "ое", "ее", "ых", "их",
                "ть", "ти", "чь", "ешь", "ёшь", "ет", "ёт",
                "ем", "ём", "ете", "ёте", "ут", "ют", "ат", "ят",
                "л", "ла", "ло", "ли", "ться", "лся", "лась", "лось",
                "ый", "ий", "ое", "ее", "ом", "ем", "ах", "ях",
                "ами", "ями", "ах", "ях", "ой", "ей", "ою", "ею",
                "ами", "ями", "ах", "ях", "ой", "ей", "ою", "ею",
                "ами", "ями", "ах", "ях", "ой", "ей", "ою", "ею",
                "а", "я", "о", "е", "у", "ю", "ы", "и", "ь", "ъ"
            };

            foreach (var ending in endings.OrderByDescending(e => e.Length))
            {
                if (word.EndsWith(ending) && word.Length > ending.Length + 2)
                {
                    return word.Substring(0, word.Length - ending.Length);
                }
            }

            return word;
        }

        public List<string> ExpandQuery(List<string> tokens)
        {
            var expanded = new HashSet<string>();

            foreach (var token in tokens)
            {
                var normalized = NormalizeWord(token);
                if (!string.IsNullOrEmpty(normalized))
                {
                    expanded.Add(normalized);

                    // добавление синонимов
                    if (_synonyms.TryGetValue(normalized, out var synonyms))
                    {
                        foreach (var syn in synonyms)
                        {
                            expanded.Add(syn);
                        }
                    }

                    expanded.Add(token.ToLower());
                }
            }

            return expanded.ToList();
        }
    }
}

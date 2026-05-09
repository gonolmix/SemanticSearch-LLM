using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SemanticSearch.Application.AdditionalClasses;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Infrastructure.AdditionalClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Services
{
    public class SynonymApiService : ISynonymApiService
    {
        private readonly ILogger<SynonymApiService> _logger;

        public SynonymApiService(ILogger<SynonymApiService> logger)
        {
            _logger = logger;
        }

        public Task<List<SynonymResult>> GetSynonymsAsync(string word, int maxResults = 10)
        {
            // 🔥 Datamuse не поддерживает русский — сразу возвращаем пустой список
            _logger?.LogDebug($"Skipping API synonym lookup for '{word}' (Russian not supported by Datamuse)");
            return Task.FromResult(new List<SynonymResult>());
        }

        public Task<List<SynonymResult>> GetSynonymsWithCacheAsync(string word, int maxResults = 10)
        {
            return GetSynonymsAsync(word, maxResults);
        }
    }
    //public class SynonymApiService : ISynonymApiService
    //{
    //    private readonly HttpClient _httpClient;
    //    private readonly IMemoryCache _cache;
    //    private readonly ILogger<SynonymApiService> _logger;

    //    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(24);
    //    private const string DatamuseBaseUrl = "https://api.datamuse.com/words";

    //    public SynonymApiService(
    //        HttpClient httpClient,
    //        IMemoryCache cache,
    //        ILogger<SynonymApiService> logger)
    //    {
    //        _httpClient = httpClient;
    //        _cache = cache;
    //        _logger = logger;
    //    }

    //    public async Task<List<SynonymResult>> GetSynonymsAsync(string word, int maxResults = 10)
    //    {
    //        if (string.IsNullOrWhiteSpace(word) || word.Length < 3)
    //            return new List<SynonymResult>();

    //        try
    //        {
    //            // Datamuse API: rel_syn = синонимы
    //            var url = $"{DatamuseBaseUrl}?rel_syn={Uri.EscapeDataString(word)}&max={maxResults}";

    //            var response = await _httpClient.GetAsync(url);

    //            if (!response.IsSuccessStatusCode)
    //            {
    //                _logger.LogWarning($"Datamuse API returned {response.StatusCode} for '{word}'");
    //                return new List<SynonymResult>();
    //            }

    //            var results = await response.Content.ReadFromJsonAsync<List<DatamuseResponse>>();

    //            return results?.Select(r => new SynonymResult
    //            {
    //                Word = r.Word,
    //                SimilarityScore = r.Score / 100.0, // Нормализуем к 0-1
    //                Source = "datamuse"
    //            }).ToList() ?? new List<SynonymResult>();
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, $"Error fetching synonyms for '{word}'");
    //            return new List<SynonymResult>(); // Graceful degradation
    //        }
    //    }

    //    public async Task<List<SynonymResult>> GetSynonymsWithCacheAsync(string word, int maxResults = 10)
    //    {
    //        var cacheKey = $"syn_api_{word.ToLower().Trim()}";

    //        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
    //        {
    //            entry.SetAbsoluteExpiration(_cacheDuration);

    //            var synonyms = await GetSynonymsAsync(word, maxResults);

    //            _logger.LogDebug($"Cached {synonyms.Count} synonyms for '{word}'");

    //            return synonyms;
    //        });
    //    }
    //}
}

using Microsoft.Extensions.Caching.Memory;
using SemanticSearch.Application.IServices;
using SemanticSearch.Core.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Services
{
    public class CachedSearchService : ISearchService
    {
        private readonly ISearchService _innerService;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

        public CachedSearchService(ISearchService innerService, IMemoryCache cache)
        {
            _innerService = innerService;
            _cache = cache;
        }

        public async Task<SearchResultDto> SearchAsync(string query)
        {
            string cacheKey = $"search_{query.ToLower().Trim()}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SetAbsoluteExpiration(_cacheDuration);
                return await _innerService.SearchAsync(query);
            });
        }
    }
}

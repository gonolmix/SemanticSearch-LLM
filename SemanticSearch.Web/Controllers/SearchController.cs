using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Application.Services;
using SemanticSearch.Core.DTO;
using SemanticSearch.Core.Enums;

namespace SemanticSearch.Web.Controllers
{
    public class SearchController : Controller
    {
        private readonly ISemanticSearchService _searchService;
        private readonly ScholarlyImportService _importService;
        private readonly ILogger<SearchController> _logger;

        private static readonly HashSet<string> QuestionWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "how", "what", "why", "when", "where", "who", "which", "whose", "whom",
            "does", "do", "did", "is", "are", "was", "were", "be", "been", "being",
            "can", "could", "will", "would", "shall", "should", "may", "might", "must",
            "the", "a", "an", "in", "on", "at", "to", "for", "of", "and", "or", "but",
            "this", "that", "these", "those", "it", "its", "from", "by", "with"
        };

        public SearchController(
            ISemanticSearchService searchService,
            ScholarlyImportService importService,
            ILogger<SearchController> logger)
        {
            _searchService = searchService;
            _importService = importService;
            _logger = logger;
        }

        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Search(
            string query,
            SearchAlgorithm algorithm = SearchAlgorithm.Hybrid,
            string[]? sources = null,
            bool liveImport = false)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ModelState.AddModelError("", "Введите поисковый запрос");
                return View("Index");
            }

            try
            {
                _logger.LogInformation(
                    "Searching for: '{Query}' using {Algorithm} (liveImport={LiveImport}, sources={Sources})",
                    query, algorithm, liveImport, sources is null ? "all" : string.Join(",", sources));



                if (liveImport)
                {
                    var apiSources = sources?
                        .Where(s => s != "local" &&
                                   (s == "openalex" || s == "semanticscholar" || s == "arxiv" || s == "europepmc" || s == "core"))
                        .ToList() ?? new List<string>();

                    if (apiSources.Any())
                    {
                        var simplifiedQuery = SimplifyQueryForApi(query);
                        _logger.LogInformation(
                            "Live import triggered. Original: '{Original}' → Simplified: '{Simplified}'. Sources: {Sources}",
                            query, simplifiedQuery, string.Join(",", apiSources));

                        await _importService.ImportAsync(
                            simplifiedQuery,
                            apiSources,
                            takePerSource: 10,
                            HttpContext.RequestAborted);
                    }
                }
                var request = new SearchRequestDto
                {
                    Query = query.Trim(),
                    Algorithm = algorithm,
                    TopK = 10,
                    UseCache = false,
                    LogQuery = true,
                    SourceFilter = sources is { Length: > 0 } ? sources : null  
                };
                // 2. Поиск по локальной БД (включающий только что импортированное)
                var response = await _searchService.SearchAsync(request);

                return View("Result", response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search failed");
                ViewBag.ErrorMessage = $"Ошибка поиска: {ex.Message}";
                return View("Error");
            }
        }

        /// <summary>
        /// Убирает question-слова и лишние знаки для поиска в API (OpenAlex/S2 лучше работают с ключевыми словами).
        /// </summary>
        private static string SimplifyQueryForApi(string query)
        {
            var words = query
                .Split(new[] { ' ', '\t', '\n', '\r', '?', '!', ',', '.', ';', ':', '"', '\'', '(', ')' },
                       StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !QuestionWords.Contains(w))
                .ToList();

            // Если ничего не осталось — возвращение исходного запроса (обрезать до 60 символов)
            return words.Any() ? string.Join(" ", words) : query.Trim().Substring(0, Math.Min(query.Length, 60));
        }
    }
}
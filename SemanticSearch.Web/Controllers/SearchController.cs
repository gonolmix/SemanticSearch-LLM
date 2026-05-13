using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Core.DTO;
using SemanticSearch.Core.Enums;
using System.Diagnostics;

namespace SemanticSearch.Web.Controllers
{
    public class SearchController : Controller
    {
        private readonly ISemanticSearchService _searchService;
        private readonly ILogger<SearchController> _logger;

        public SearchController(
            ISemanticSearchService searchService,
            ILogger<SearchController> logger)
        {
            _searchService = searchService;
            _logger = logger;
        }

        /// <summary>
        /// Главная страница с формой поиска
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Обработка запроса поиска
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Search(string query, SearchAlgorithm algorithm = SearchAlgorithm.Hybrid)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ModelState.AddModelError("", "Введите поисковый запрос");
                return View("Index");
            }

            try
            {
                _logger.LogInformation($"Searching for: '{query}' using {algorithm}");

                var request = new SearchRequestDto
                {
                    Query = query.Trim(),
                    Algorithm = algorithm,
                    TopK = 5,
                    UseCache = false,
                    LogQuery = true
                };

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
    }
}
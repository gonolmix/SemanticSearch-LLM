using Microsoft.AspNetCore.Mvc;
using SemanticSearch.Application.Services;

namespace SemanticSearch.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImportController : ControllerBase
    {
        private readonly ScholarlyImportService _importService;
        private readonly ILogger<ImportController> _logger;

        public ImportController(
            ScholarlyImportService importService,
            ILogger<ImportController> logger)
        {
            _importService = importService;
            _logger = logger;
        }

        /// <summary>
        /// Импортирует научные статьи из выбранных источников по запросу
        /// </summary>
        /// <param name="request">Параметры импорта</param>
        [HttpPost]
        public async Task<IActionResult> Import([FromBody] ImportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest("Query cannot be empty");
            }

            if (request.Sources is null || !request.Sources.Any())
            {
                return BadRequest("At least one source must be selected");
            }

            var takePerSource = request.TakePerSource > 0 ? request.TakePerSource : 25;

            _logger.LogInformation(
                "Starting import: query='{Query}', sources={Sources}, take={Take}",
                request.Query,
                string.Join(",", request.Sources),
                takePerSource);

            var report = await _importService.ImportAsync(
                request.Query,
                request.Sources,
                takePerSource,
                HttpContext.RequestAborted);

            return Ok(report);
        }

        /// <summary>
        /// Получает список доступных источников
        /// </summary>
        [HttpGet("sources")]
        public IActionResult GetSources()
        {
            var sources = new[]
            {
                new { Name = "openalex", DisplayName = "OpenAlex", Enabled = true },
                new { Name = "semanticscholar", DisplayName = "Semantic Scholar", Enabled = true }
            };

            return Ok(sources);
        }
    }

    public sealed class ImportRequest
    {
        public required string Query { get; set; }
        public required IReadOnlyList<string> Sources { get; set; }
        public int TakePerSource { get; set; } = 25;
    }
}
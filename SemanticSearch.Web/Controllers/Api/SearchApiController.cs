using Microsoft.AspNetCore.Mvc;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Core.DTO;
using SemanticSearch.Core.Enums;
using System.Text.Json.Serialization;  

namespace SemanticSearch.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [IgnoreAntiforgeryToken]
    public class SearchApiController : ControllerBase
    {
        private readonly ISemanticSearchService _searchService;
        private readonly ILogger<SearchApiController> _logger; 

        public SearchApiController(
            ISemanticSearchService searchService,
            ILogger<SearchApiController> logger)  
        {
            _searchService = searchService;
            _logger = logger;
        }

        [HttpPost("search")]
        [ProducesResponseType(typeof(SearchResponseDto), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<SearchResponseDto>> Search([FromBody] SearchRequestDto request)
        {
            _logger.LogInformation($"API Search: Query='{request?.Query}', Algorithm={request?.Algorithm}");

            if (string.IsNullOrWhiteSpace(request?.Query))
            {
                _logger.LogWarning("Search request missing query");
                return BadRequest(new { error = "Query is required" });
            }

            try
            {
                var result = await _searchService.SearchAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search API error");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("health")]
        public ActionResult<HealthResponse> Health()
        {
            return Ok(new HealthResponse
            {
                Status = "ok",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public class HealthResponse
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
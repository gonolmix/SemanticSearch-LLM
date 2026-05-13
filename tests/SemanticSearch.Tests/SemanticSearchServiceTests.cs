using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Application.Services;
using SemanticSearch.Core.DTO;
using SemanticSearch.Core.Enums;
using SemanticSearch.Infrastructure.AdditionalClasses;
using SemanticSearch.Infrastructure.Data;
using SemanticSearch.Infrastructure.Repositories;
using SemanticSearch.Infrastructure.VectorStore;
using Xunit;

namespace SemanticSearch.Tests
{
    public class SemanticSearchServiceTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly Mock<IEmbeddingService> _mockEmbedding;
        private readonly Mock<IRankingService> _mockRanking;
        private readonly Mock<ILinguisticService> _mockLinguistic;
        private readonly IMemoryCache _cache;
        private readonly Mock<ILogger<SemanticSearchService>> _mockLogger;
        private readonly SemanticSearchService _service;

        public SemanticSearchServiceTests()
        {
            // Создание in-memory базу для тестов
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            _dbContext = new AppDbContext(options);
            _dbContext.Database.EnsureCreated(); // таблицы в памяти

            _mockEmbedding = new Mock<IEmbeddingService>();
            _mockRanking = new Mock<IRankingService>();
            _mockLinguistic = new Mock<ILinguisticService>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _mockLogger = new Mock<ILogger<SemanticSearchService>>();

            _mockEmbedding.Setup(e => e.VectorDimension).Returns(384);
            _mockEmbedding.Setup(e => e.ModelName).Returns("all-MiniLM-L6-v2");
            _mockEmbedding.Setup(e => e.IsReady).Returns(true);

            var paragraphRepo = new ParagraphRepository(_dbContext);
            var vectorRepo = new VectorRepository(_dbContext);

            var mockVectorStore = new Mock<IVectorStore>();
            mockVectorStore.Setup(v => v.InitializeAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);
            mockVectorStore.Setup(v => v.AddVectorAsync(It.IsAny<int>(), It.IsAny<float[]>()))
                .Returns(Task.CompletedTask);
            mockVectorStore.Setup(v => v.SearchSimilarAsync(It.IsAny<float[]>(), It.IsAny<int>()))
                .ReturnsAsync(new List<(int, float)>());

            // Создаём сервис
            _service = new SemanticSearchService(
                _mockEmbedding.Object,
                _mockRanking.Object,
                _mockLinguistic.Object,
                paragraphRepo,
                vectorRepo,
                mockVectorStore.Object,
                _cache,
                _mockLogger.Object);
        }

        [Fact]
        public async Task SearchAsync_EmptyQuery_ReturnsEmptyResults()
        {
            // Arrange
            var request = new SearchRequestDto { Query = "", Algorithm = SearchAlgorithm.Vector };

            _mockRanking.Setup(r => r.RankAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<Core.Entities.Paragraph>>(),
                    It.IsAny<float[]>(),
                    It.IsAny<SearchAlgorithm>()))
                .ReturnsAsync(new List<ParagraphScore>());

            // Act
            var result = await _service.SearchAsync(request);

            // Assert
            Assert.Empty(result.Matches);
            Assert.Equal("", result.Query);
        }

        [Fact]
        public async Task SearchAsync_CacheHit_ReturnsCachedResult()
        {
            // Arrange
            var request = new SearchRequestDto
            {
                Query = "test",
                Algorithm = SearchAlgorithm.Vector,
                UseCache = true
            };

            var cachedResult = new SearchResponseDto
            {
                Query = "test",
                Matches = new List<SearchMatchDto>
                {
                    new SearchMatchDto { DocumentTitle = "Cached Doc" }
                }
            };

            _cache.Set($"search_Vector_test", cachedResult, TimeSpan.FromMinutes(30));

            // Act
            var result = await _service.SearchAsync(request);

            // Assert
            Assert.True(result.FromCache);
            Assert.Single(result.Matches);
            Assert.Equal("Cached Doc", result.Matches[0].DocumentTitle);
        }

        public void Dispose()
        {
            _dbContext?.Database.EnsureDeleted(); // очистка in-memory базы
            _dbContext?.Dispose();
            _cache?.Dispose();
        }
    }
}
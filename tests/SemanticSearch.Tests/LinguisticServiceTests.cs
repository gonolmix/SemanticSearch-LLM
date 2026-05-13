using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SemanticSearch.Application.AdditionalClasses;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Infrastructure.Data;
using SemanticSearch.Infrastructure.Repositories;
using Xunit;

namespace SemanticSearch.Tests
{
    public class LinguisticServiceTests : IDisposable
    {
        private readonly AppDbContext _dbContext;
        private readonly LinguisticService _service;

        public LinguisticServiceTests()
        {
            // In-memory база для тестов
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"LinguisticTestDb_{Guid.NewGuid()}")
                .Options;

            _dbContext = new AppDbContext(options);
            _dbContext.Database.EnsureCreated();

            var mockApi = new Mock<ISynonymApiService>();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var mockLogger = new Mock<ILogger<LinguisticService>>();

            var repo = new LinguisticRepository(_dbContext);

            _service = new LinguisticService(repo, mockApi.Object, cache, mockLogger.Object);
        }

        [Fact]
        public async Task LoadDataAsync_LoadsStopWords()
        {
            // Arrange
            await _dbContext.StopWords.AddRangeAsync(
                new Core.Entities.StopWord { Word = "the" },
                new Core.Entities.StopWord { Word = "is" });
            await _dbContext.SaveChangesAsync();

            // Act
            await _service.LoadDataAsync();

            // Assert
            Assert.True(_service.IsStopWord("the"));
            Assert.True(_service.IsStopWord("THE")); // case-insensitive
            Assert.False(_service.IsStopWord("cat"));
        }

        [Fact]
        public void Tokenize_RemovesPunctuation()
        {
            var tokens = _service.Tokenize("Hello, World! Test.");
            Assert.Equal(new[] { "hello", "world", "test" }, tokens);
        }

        [Fact]
        public void Tokenize_FiltersStopWords()
        {
            // Arrange
            _dbContext.StopWords.Add(new Core.Entities.StopWord { Word = "the" });
            _dbContext.SaveChanges();
            _service.LoadDataAsync().Wait();

            // Act
            var tokens = _service.Tokenize("the cat");

            // Assert
            Assert.DoesNotContain("the", tokens);
            Assert.Contains("cat", tokens);
        }

        public void Dispose()
        {
            _dbContext?.Database.EnsureDeleted();
            _dbContext?.Dispose();
        }
    }
}
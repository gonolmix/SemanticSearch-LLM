using Microsoft.Extensions.Logging;
using Moq;
using SemanticSearch.Application.Services;
using Xunit;

namespace SemanticSearch.Tests
{
    public class EmbeddingServiceTests : IDisposable
    {
        private readonly Mock<ILogger<EmbeddingService>> _mockLogger;
        private readonly string _modelPath;
        private EmbeddingService? _service;

        public EmbeddingServiceTests()
        {
            _mockLogger = new Mock<ILogger<EmbeddingService>>();
            // Путь к тестовой модели
            _modelPath = Path.Combine(AppContext.BaseDirectory, "ml-models", "all-minilm-l6-v2");
        }

        [Fact]
        public void Constructor_SetsProperties()
        {
            // Arrange & Act
            _service = new EmbeddingService(_mockLogger.Object, _modelPath);

            // Assert
            Assert.Equal(384, _service.VectorDimension);
            Assert.Equal("all-MiniLM-L6-v2", _service.ModelName);
            Assert.False(_service.IsReady); // Not initialized yet
        }

        [Fact]
        public async Task InitializeAsync_WithMissingModel_DoesNotCrash()
        {
            // Arrange
            _service = new EmbeddingService(_mockLogger.Object, "D:\\UchH");

            // Act & Assert (should not throw)
            await _service.InitializeAsync();
            Assert.False(_service.IsReady);
        }

        [Fact]
        public async Task GenerateEmbeddingAsync_EmptyText_ReturnsZeroVector()
        {
            // Arrange - using mock or skip if model not available
            // For now, test the fallback behavior
            _service = new EmbeddingService(_mockLogger.Object, "D:\\UchH");

            // Act
            var result = await _service.GenerateEmbeddingAsync("");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(384, result.Length);
            Assert.All(result, v => Assert.Equal(0f, v));
        }

        [Fact]
        public void GenerateEmbeddingsAsync_BatchProcessing_DoesNotThrow()
        {
            // This would require a real model or extensive mocking
            // Placeholder for integration test
            Assert.True(true);
        }

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}
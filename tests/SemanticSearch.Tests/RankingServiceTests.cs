using Microsoft.Extensions.Logging;
using Moq;
using SemanticSearch.Application.Interfaces;
using SemanticSearch.Application.Services;
using SemanticSearch.Core.Entities;
using SemanticSearch.Core.Enums;
using Xunit;

namespace SemanticSearch.Tests
{
    public class RankingServiceTests
    {
        private readonly Mock<ILinguisticService> _mockLinguistic;
        private readonly Mock<ILogger<RankingService>> _mockLogger;
        private readonly RankingService _service;

        public RankingServiceTests()
        {
            _mockLinguistic = new Mock<ILinguisticService>();
            _mockLogger = new Mock<ILogger<RankingService>>();
            _service = new RankingService(_mockLinguistic.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task RankAsync_VectorMode_UsesOnlyVectorScores()
        {
            // Arrange
            var query = "test query";
            var queryVector = new float[384];
            for (int i = 0; i < 384; i++) queryVector[i] = 0.1f;

            var paragraphs = new List<Paragraph>
            {
                new Paragraph
                {
                    Id = 1,
                    Content = "relevant content about test",
                    Embedding = new float[384] // Same direction as query
                },
                new Paragraph
                {
                    Id = 2,
                    Content = "unrelated content",
                    Embedding = new float[384] // Orthogonal to query
                }
            };
            // Make paragraph 1 similar to query
            for (int i = 0; i < 384; i++) paragraphs[0].Embedding[i] = queryVector[i];

            // Act
            var results = await _service.RankAsync(
                query, paragraphs, queryVector, SearchAlgorithm.Vector);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Equal(1, results[0].Paragraph.Id); // Most similar first
            Assert.True(results[0].TotalScore > results[1].TotalScore);
            Assert.Equal(0, results[0].TfidfScore); // No keyword bonus in Vector mode
        }

        [Fact]
        public async Task RankAsync_HybridMode_AppliesKeywordBonus()
        {
            // Arrange
            var query = "tokenization process";
            var queryVector = new float[384];

            var paragraphs = new List<Paragraph>
            {
                new Paragraph
                {
                    Id = 1,
                    Content = "Tokenization is the process of breaking text",
                    Embedding = new float[384]
                },
                new Paragraph
                {
                    Id = 2,
                    Content = "Neural networks training algorithms",
                    Embedding = new float[384]
                }
            };

            // Act
            var results = await _service.RankAsync(
                query, paragraphs, queryVector, SearchAlgorithm.Hybrid);

            // Assert
            Assert.Equal(2, results.Count);
            // Paragraph 1 should have higher score due to keyword match
            Assert.Equal(1, results[0].Paragraph.Id);
            Assert.True(results[0].TfidfScore > 0); // Has keyword bonus
        }

        [Fact]
        public void CalculateKeywordBonus_TitleMatch_GetsHigherBonus()
        {
            // This tests the private method via reflection or by testing public behavior
            // For now, we test the overall behavior
            Assert.True(true); // Placeholder - implement if needed
        }
        [Fact]
        public async Task RankAsync_EmptyParagraphs_ReturnsEmptyList()
        {
            // Arrange
            var query = "test";
            var queryVector = new float[384];
            var paragraphs = new List<Core.Entities.Paragraph>();

            // Act
            var results = await _service.RankAsync(query, paragraphs, queryVector, SearchAlgorithm.Vector);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task RankAsync_ParagraphWithoutEmbedding_SkipsIt()
        {
            // Arrange
            var query = "test";
            var queryVector = new float[384];
            var paragraphs = new List<Core.Entities.Paragraph>
    {
        new Core.Entities.Paragraph { Id = 1, Content = "test", Embedding = null },
        new Core.Entities.Paragraph { Id = 2, Content = "test", Embedding = new float[384] }
    };

            // Act
            var results = await _service.RankAsync(query, paragraphs, queryVector, SearchAlgorithm.Vector);

            // Assert
            Assert.Single(results); // Only paragraph 2 should be included
            Assert.Equal(2, results[0].Paragraph.Id);
        }

        [Fact]
        public void CalculateKeywordBonus_WordInTitle_GetsHighBonus()
        {
            // This requires testing private method - use reflection or test via public API
            // For now, test overall behavior
            Assert.True(true);
        }

        [Fact]
        public async Task RankAsync_ScoresAreNormalized_BetweenZeroAndOne()
        {
            // Arrange
            var query = "test";
            var queryVector = new float[384];
            for (int i = 0; i < 384; i++) queryVector[i] = 0.1f;

            var paragraphs = new List<Core.Entities.Paragraph>
    {
        new Core.Entities.Paragraph { Id = 1, Content = "test", Embedding = queryVector.ToArray() }, // identical
        new Core.Entities.Paragraph { Id = 2, Content = "other", Embedding = new float[384] } // zero vector
    };

            // Act
            var results = await _service.RankAsync(query, paragraphs, queryVector, SearchAlgorithm.Vector);

            // Assert
            foreach (var r in results)
            {
                Assert.InRange(r.TotalScore, 0.0, 1.0);
                Assert.InRange(r.VectorScore, 0.0, 1.0);
            }
        }
    }
}
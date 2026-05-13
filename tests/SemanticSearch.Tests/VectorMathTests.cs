using SemanticSearch.Application.Helpers;
using Xunit;

namespace SemanticSearch.Tests
{
    public class VectorMathTests
    {
        [Fact]
        public void CosineSimilarity_IdenticalVectors_ReturnsOne()
        {
            // Arrange
            var vector = new float[] { 1f, 2f, 3f, 4f };

            // Act
            var result = VectorMath.CosineSimilarity(vector, vector);

            // Assert
            Assert.Equal(1.0f, result, 3); // 3 decimal places
        }

        [Fact]
        public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
        {
            // Arrange
            var a = new float[] { 1f, 0f, 0f, 0f };
            var b = new float[] { 0f, 1f, 0f, 0f };

            // Act
            var result = VectorMath.CosineSimilarity(a, b);

            // Assert
            Assert.Equal(0.0f, result, 3);
        }

        [Fact]
        public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
        {
            // Arrange
            var a = new float[] { 1f, 2f, 3f };
            var b = new float[] { -1f, -2f, -3f };

            // Act
            var result = VectorMath.CosineSimilarity(a, b);

            // Assert
            Assert.Equal(-1.0f, result, 3);
        }

        [Fact]
        public void Normalize_UnitVector_ReturnsSame()
        {
            // Arrange
            var vector = new float[] { 1f, 0f, 0f };

            // Act
            var result = VectorMath.Normalize(vector);

            // Assert
            Assert.Equal(1f, result[0], 3);
            Assert.Equal(0f, result[1], 3);
            Assert.Equal(0f, result[2], 3);
        }

        [Fact]
        public void Normalize_ArbitraryVector_ReturnsUnitLength()
        {
            // Arrange
            var vector = new float[] { 3f, 4f }; // length = 5

            // Act
            var result = VectorMath.Normalize(vector);

            // Assert
            var length = MathF.Sqrt(result[0] * result[0] + result[1] * result[1]);
            Assert.Equal(1f, length, 3);
            Assert.Equal(0.6f, result[0], 3); // 3/5
            Assert.Equal(0.8f, result[1], 3); // 4/5
        }

        [Fact]
        public void BytesToFloats_ConvertsCorrectly()
        {
            // Arrange
            var floats = new float[] { 1.5f, -2.5f, 0.0f };
            var bytes = VectorMath.FloatsToBytes(floats);

            // Act
            var result = VectorMath.BytesToFloats(bytes);

            // Assert
            Assert.Equal(floats.Length, result.Length);
            Assert.Equal(floats[0], result[0], 3);
            Assert.Equal(floats[1], result[1], 3);
            Assert.Equal(floats[2], result[2], 3);
        }

        [Fact]
        public void FloatsToBytes_RoundTrip_PreservesData()
        {
            // Arrange
            var original = new float[] { 0.123f, -0.456f, 0.789f };

            // Act
            var bytes = VectorMath.FloatsToBytes(original);
            var result = VectorMath.BytesToFloats(bytes);

            // Assert
            Assert.Equal(original.Length, result.Length);
            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], result[i], 3);
            }
        }
    }
}
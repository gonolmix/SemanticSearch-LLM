// Application/Services/MockEmbeddingService.cs
using SemanticSearch.Application.Helpers;
using SemanticSearch.Application.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SemanticSearch.Application.Services
{
    /// <summary>
    /// Заглушка для тестов - генерирует случайные векторы
    /// </summary>
    public class MockEmbeddingService : IEmbeddingService
    {
        private readonly Random _random = new(42);
        public int VectorDimension => 768;
        public string ModelName => "mock-embedding-v1";
        public bool IsReady => true;

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            // Детерминированный "эмбеддинг" на основе хэша текста
            var hash = text.GetHashCode();
            var vector = new float[VectorDimension];

            for (int i = 0; i < VectorDimension; i++)
            {
                vector[i] = (float)Math.Sin(hash + i) * 0.1f;
            }

            return Task.FromResult(VectorMath.Normalize(vector));
        }

        public Task<float[][]> GenerateEmbeddingsAsync(string[] texts)
        {
            var results = new float[texts.Length][];
            for (int i = 0; i < texts.Length; i++)
            {
                results[i] = GenerateEmbeddingAsync(texts[i]).Result;
            }
            return Task.FromResult(results);
        }

        public void Dispose() { }
    }
}
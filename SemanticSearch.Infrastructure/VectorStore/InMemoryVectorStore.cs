using SemanticSearch.Application.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Infrastructure.VectorStore
{
    public class InMemoryVectorStore : IVectorStore
    {
        private readonly ConcurrentDictionary<int, float[]> _vectors = new();
        private int _dimension = 768;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public Task InitializeAsync(int dimension)
        {
            _dimension = dimension;
            return Task.CompletedTask;
        }

        public Task AddVectorAsync(int paragraphId, float[] vector)
        {
            if (vector.Length != _dimension)
                throw new ArgumentException($"Vector dimension must be {_dimension}");

            // Нормализуем вектор для косинусного сходства
            var normalized = VectorMath.Normalize(vector);
            _vectors[paragraphId] = normalized;
            return Task.CompletedTask;
        }

        public async Task AddVectorsAsync(IEnumerable<(int ParagraphId, float[] Vector)> vectors)
        {
            foreach (var (paragraphId, vector) in vectors)
            {
                await AddVectorAsync(paragraphId, vector);
            }
        }

        public Task<List<(int ParagraphId, float Score)>> SearchSimilarAsync(float[] queryVector, int topK)
        {
            if (_vectors.IsEmpty)
                return Task.FromResult(new List<(int, float)>());

            // Нормализуем запрос
            var normalizedQuery = VectorMath.Normalize(queryVector);

            // Параллельный расчёт косинусного сходства
            var results = _vectors
                .AsParallel()
                .Select(kvp => (
                    ParagraphId: kvp.Key,
                    Score: VectorMath.CosineSimilarity(normalizedQuery, kvp.Value)
                ))
                .OrderByDescending(r => r.Score)
                .Take(topK)
                .ToList();

            return Task.FromResult(results);
        }

        public Task RemoveVectorAsync(int paragraphId)
        {
            _vectors.TryRemove(paragraphId, out _);
            return Task.CompletedTask;
        }

        public Task<bool> ContainsAsync(int paragraphId)
        {
            return Task.FromResult(_vectors.ContainsKey(paragraphId));
        }

        public Task<int> CountAsync()
        {
            return Task.FromResult(_vectors.Count);
        }

        public Task ClearAsync()
        {
            _vectors.Clear();
            return Task.CompletedTask;
        }
    }
}

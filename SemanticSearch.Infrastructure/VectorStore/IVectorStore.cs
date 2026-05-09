using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Infrastructure.VectorStore
{
    public interface IVectorStore
    {
        Task InitializeAsync(int dimension);
        Task AddVectorAsync(int paragraphId, float[] vector);
        Task AddVectorsAsync(IEnumerable<(int ParagraphId, float[] Vector)> vectors);
        Task<List<(int ParagraphId, float Score)>> SearchSimilarAsync(float[] queryVector, int topK);
        Task RemoveVectorAsync(int paragraphId);
        Task<bool> ContainsAsync(int paragraphId);
        Task<int> CountAsync();
        Task ClearAsync();
    }
}

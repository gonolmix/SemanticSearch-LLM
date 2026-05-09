using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.DTO
{
    public class IndexingStatsDto
    {
        public int TotalParagraphs { get; set; }
        public int IndexedParagraphs { get; set; }
        public int PendingParagraphs { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public int VectorDimension { get; set; }
    }
}

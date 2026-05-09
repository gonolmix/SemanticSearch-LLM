using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.DTO
{
    public class DocumentStatsDto
    {
        public int TotalParagraphs { get; set; }
        public int IndexedParagraphs { get; set; }
        public int TotalWords { get; set; }
        public int TotalChars { get; set; }
        public DateTime? LastIndexedAt { get; set; }
    }
}

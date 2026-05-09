using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.DTO
{
    public class ParagraphDto
    {
        public int Id { get; set; }

        public int DocumentId { get; set; }

        public string Content { get; set; } = string.Empty;

        public int ParagraphOrder { get; set; }

        public int WordCount { get; set; }

        public DateTime? IndexedAt { get; set; }

        public bool HasVector { get; set; }
    }
}

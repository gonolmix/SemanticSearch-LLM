using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.DTO
{
    public class DocumentDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? SourceUrl { get; set; }

        public string SourceType { get; set; } = "manual";

        public DateTime CreatedAt { get; set; }

        public int ParagraphCount { get; set; }

        public bool IsIndexed { get; set; }

        public List<ParagraphDto> Paragraphs { get; set; } = new List<ParagraphDto>();
    }
}

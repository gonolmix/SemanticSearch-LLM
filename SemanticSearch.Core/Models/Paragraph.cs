using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Models
{
    public class Paragraph
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public Document? Document { get; set; }
        public string Content { get; set; } = string.Empty;
        public int ParagraphOrder { get; set; }

        [NotMapped]
        public List<string>? ProcessedTokens { get; set; }
    }
}

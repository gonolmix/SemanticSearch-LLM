using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.DTO
{
    public class SearchMatchDto
    {
        public int ParagraphId { get; set; }

        public int DocumentId { get; set; }

        public string DocumentTitle { get; set; } = string.Empty;

        public string ParagraphContent { get; set; } = string.Empty;

        public double RelevanceScore { get; set; }

        public double VectorScore { get; set; }

        public double KeywordScore { get; set; }

        public int Rank { get; set; }

        public List<string> HighlightedWords { get; set; } = new List<string>();

        public Dictionary<string, double>? ScoreBreakdown { get; set; }
    }
}

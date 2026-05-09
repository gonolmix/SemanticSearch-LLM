using SemanticSearch.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Infrastructure.AdditionalClasses
{
    public class ParagraphScore
    {
        public Paragraph Paragraph { get; set; } = null!;
        public double TotalScore { get; set; }
        public double TfidfScore { get; set; }
        public double BM25Score { get; set; }
        public double VectorScore { get; set; }
        public List<string> MatchedTerms { get; set; } = new();
    }
}

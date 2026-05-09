using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Infrastructure.AdditionalClasses
{
    public class SynonymResult
    {
        public string Word { get; set; } = string.Empty;
        public double SimilarityScore { get; set; }
        public string Source { get; set; } = string.Empty; // "datamuse", "api", etc.
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Classes
{
    public class ParagraphStats
    {
        public Dictionary<string, int> TermFrequency { get; set; }
        public int TotalTerms { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Models
{
    public class Synonym
    {
        public int Id { get; set; }
        public string SourceWord { get; set; } = string.Empty;
        public string TargetWord { get; set; } = string.Empty;
    }
}

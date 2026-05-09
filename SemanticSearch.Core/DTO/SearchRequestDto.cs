using SemanticSearch.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.DTO
{
    public class SearchRequestDto
    {
        public string Query { get; set; } = string.Empty;

        public SearchAlgorithm Algorithm { get; set; } = SearchAlgorithm.Hybrid;

        public int TopK { get; set; } = 10;

        public double? MinScore { get; set; }

        public bool UseCache { get; set; } = true;

        public bool LogQuery { get; set; } = true;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.DTO
{
    public class SearchResponseDto
    {
        public string Query { get; set; } = string.Empty;

        public string AlgorithmUsed { get; set; } = string.Empty;

        public List<SearchMatchDto> Matches { get; set; } = new List<SearchMatchDto>();

        public int TotalTimeMs { get; set; }

        public int VectorSearchTimeMs { get; set; }

        public int KeywordSearchTimeMs { get; set; }

        public int TotalResults { get; set; }

        public bool FromCache { get; set; }
    }
}

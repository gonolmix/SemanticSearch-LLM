using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.DTO
{
    public class SearchResultDto
    {
        public string Query { get; set; } = string.Empty;
        public List<SearchMatchDto> Matches { get; set; } = new List<SearchMatchDto>();
        public int TotalTimeMs { get; set; }
    }
}

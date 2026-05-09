using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.AdditionalClasses
{
    public class DatamuseResponse
    {
        public string Word { get; set; } = string.Empty;
        public int Score { get; set; }
        public int[]? Tags { get; set; }
    }
}

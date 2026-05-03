using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SemanticSearch.Core.DTO;

namespace SemanticSearch.Application.IServices
{
    public interface ISearchService
    {
        Task<SearchResultDto> SearchAsync(string query);
    }
}

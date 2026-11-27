using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.ExternalSources
{
    public interface IScholarlySourceClient
    {
        string SourceName { get; }
        Task<IReadOnlyList<ScholarlyWork>> SearchAsync(string query, int take = 25, CancellationToken cancellationToken = default);
    }
}

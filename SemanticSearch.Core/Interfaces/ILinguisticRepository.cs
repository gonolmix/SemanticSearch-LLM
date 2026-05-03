using SemanticSearch.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Interfaces
{
    public interface ILinguisticRepository
    {
        Task<List<StopWord>> GetStopWordsAsync();
        Task<List<Synonym>> GetSynonymsAsync();

        Task<List<MorphologyRule>> GetMorphologyRulesAsync();
        Task<List<Paragraph>> GetAllParagraphsAsync();
    }
}

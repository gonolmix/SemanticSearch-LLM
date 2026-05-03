using Microsoft.EntityFrameworkCore;
using SemanticSearch.Core.Models;
using SemanticSearch.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Infrastructure.Repositories
{
    public class LinguisticRepository
    {
        private readonly AppDbContext _context;

        public LinguisticRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<StopWord>> GetStopWordsAsync()
        {
            return await _context.StopWords.ToListAsync();
        }

        public async Task<List<Synonym>> GetSynonymsAsync()
        {
            return await _context.Synonyms.ToListAsync();
        }

        public async Task<List<MorphologyRule>> GetMorphologyRulesAsync()
        {
            return await _context.MorphologyRules.ToListAsync();
        }

        public async Task<List<Paragraph>> GetAllParagraphsAsync()
        {
            return await _context.Paragraphs
                .Include(p => p.Document)
                .ToListAsync();
        }
    }
}

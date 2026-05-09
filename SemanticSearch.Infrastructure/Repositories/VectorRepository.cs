using Microsoft.EntityFrameworkCore;
using SemanticSearch.Core.Entities;
using SemanticSearch.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Infrastructure.Repositories
{
    public class VectorRepository : EfRepository<ParagraphVector>
    {
        public VectorRepository(AppDbContext context) : base(context) { }

        public async Task<ParagraphVector?> GetByParagraphIdAsync(int paragraphId)
        {
            return await _dbSet
                .Include(v => v.Paragraph)
                .FirstOrDefaultAsync(v => v.ParagraphId == paragraphId);
        }

        public async Task<IEnumerable<ParagraphVector>> GetAllWithParagraphsAsync()
        {
            return await _dbSet
                .Include(v => v.Paragraph)
                    .ThenInclude(p => p.Document)
                .ToListAsync();
        }

        public async Task<int> GetIndexedCountAsync()
        {
            return await _dbSet.CountAsync();
        }
    }
}

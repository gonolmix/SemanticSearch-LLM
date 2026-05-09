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
    public class ParagraphRepository : EfRepository<Paragraph>
    {
        public ParagraphRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Paragraph>> GetAllWithVectorsAsync()
        {
            return await _dbSet
                .Include(p => p.Document)
                .Include(p => p.Vector)
                .Where(p => p.Document.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<Paragraph>> GetNotIndexedAsync()
        {
            return await _dbSet
                .Include(p => p.Document)
                .Where(p => p.IndexedAt == null)
                .ToListAsync();
        }

        public async Task MarkAsIndexedAsync(int paragraphId)
        {
            var paragraph = await _dbSet.FindAsync(paragraphId);
            if (paragraph != null)
            {
                paragraph.IndexedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}

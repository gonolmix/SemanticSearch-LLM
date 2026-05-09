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
    public class DocumentRepository : EfRepository<Document>
    {
        public DocumentRepository(AppDbContext context) : base(context) { }

        public async Task<IEnumerable<Document>> GetAllWithParagraphsAsync()
        {
            return await _dbSet
                .Include(d => d.Paragraphs)
                .Where(d => d.IsActive)
                .ToListAsync();
        }

        public async Task<Document?> GetByIdWithParagraphsAsync(int id)
        {
            return await _dbSet
                .Include(d => d.Paragraphs)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task IncrementViewCountAsync(int id)
        {
            var document = await _dbSet.FindAsync(id);
            if (document != null)
            {
                document.ViewCount++;
                document.LastSearchedAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}

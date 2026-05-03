using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SemanticSearch.Core.Models;

namespace SemanticSearch.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
        public DbSet<Paragraph> Paragraphs { get; set; }
        public DbSet<StopWord> StopWords { get; set; }
        public DbSet<Synonym> Synonyms { get; set; }
        public DbSet<MorphologyRule> MorphologyRules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<StopWord>().HasIndex(s => s.Word);
            modelBuilder.Entity<Synonym>().HasIndex(s => s.SourceWord);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using SemanticSearch.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Document> Documents { get; set; }
        public DbSet<Paragraph> Paragraphs { get; set; }
        public DbSet<ParagraphVector> ParagraphVectors { get; set; }
        public DbSet<StopWord> StopWords { get; set; }
        public DbSet<Synonym> Synonyms { get; set; }
        public DbSet<SearchQueryLog> SearchQueryLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Document configuration
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
                entity.Property(e => e.SourceType).HasMaxLength(50).HasDefaultValue("manual");
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.CreatedAt);
            });

            // Paragraph configuration
            modelBuilder.Entity<Paragraph>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.WordCount).HasComputedColumnSql("LEN(Content) - LEN(REPLACE(Content, ' ', '')) + 1");
                entity.Property(e => e.CharCount).HasComputedColumnSql("LEN(Content)");
                entity.HasOne(e => e.Document)
                      .WithMany(d => d.Paragraphs)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.DocumentId);
                entity.HasIndex(e => e.IndexedAt).HasFilter("[IndexedAt] IS NOT NULL");
            });

            // ParagraphVector configuration
            modelBuilder.Entity<ParagraphVector>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ParagraphId).IsUnique();
                entity.Property(e => e.ModelName).HasMaxLength(100).HasDefaultValue("paraphrase-multilingual-MiniLM-L12-v2");
                entity.HasOne(e => e.Paragraph)
                      .WithOne(p => p.Vector)
                      .HasForeignKey<ParagraphVector>(e => e.ParagraphId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // StopWord configuration
            modelBuilder.Entity<StopWord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Word).IsUnique();
                entity.Property(e => e.Language).HasMaxLength(10).HasDefaultValue("ru");
            });

            // Synonym configuration
            modelBuilder.Entity<Synonym>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SourceWord);
                entity.Property(e => e.Source).HasMaxLength(50).HasDefaultValue("manual");
                entity.Property(e => e.Language).HasMaxLength(10).HasDefaultValue("ru");
            });

            // SearchQueryLog configuration
            modelBuilder.Entity<SearchQueryLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.QueryText).IsRequired().HasMaxLength(500);
                entity.Property(e => e.AlgorithmUsed).HasMaxLength(50);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.QueryText);
            });

            modelBuilder.Entity<Synonym>(entity =>
            {
                entity.Property(e => e.SimilarityScore)
                      .HasColumnType("decimal(3,2)");
            });
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Автоматическое обновление UpdatedAt
            foreach (var entry in ChangeTracker.Entries<Document>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}

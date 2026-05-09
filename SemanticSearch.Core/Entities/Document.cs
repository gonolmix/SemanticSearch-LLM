using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Entities
{
    public class Document
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? SourceUrl { get; set; }

        [MaxLength(50)]
        public string SourceType { get; set; } = "manual"; // manual, import, api

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int ViewCount { get; set; } = 0;

        public DateTime? LastSearchedAt { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(4000)]
        public string? Metadata { get; set; } // JSON

        // Навигационные свойства
        public ICollection<Paragraph> Paragraphs { get; set; } = new List<Paragraph>();
    }
}

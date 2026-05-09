using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Entities
{
    public class ParagraphVector
    {
        [Key]
        public int Id { get; set; }

        public int ParagraphId { get; set; }

        [ForeignKey(nameof(ParagraphId))]
        public Paragraph? Paragraph { get; set; }

        [Required]
        public byte[] VectorData { get; set; } = Array.Empty<byte>(); // Binary хранение

        public int VectorDimension { get; set; } = 768;

        [MaxLength(100)]
        public string ModelName { get; set; } = "paraphrase-multilingual-MiniLM-L12-v2";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool Normalized { get; set; } = true;
    }
}

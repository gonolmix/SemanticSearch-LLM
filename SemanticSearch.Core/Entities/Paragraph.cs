using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Entities
{
    public class Paragraph
    {
        [Key]
        public int Id { get; set; }

        public int DocumentId { get; set; }

        [ForeignKey(nameof(DocumentId))]
        public Document? Document { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public int ParagraphOrder { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int WordCount { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int CharCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? IndexedAt { get; set; } // Когда сгенерирован вектор

        public int VectorVersion { get; set; } = 1; // Версия модели эмбеддинга

        // Навигационные свойства
        public ParagraphVector? Vector { get; set; }

        // Не сохраняется в БД
        [NotMapped]
        public List<string>? ProcessedTokens { get; set; }

        [NotMapped]
        public float[]? Embedding { get; set; } // Вектор в памяти
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Entities
{
    public class Synonym
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string SourceWord { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TargetWord { get; set; } = string.Empty;

        public decimal SimilarityScore { get; set; } = 1.00m;

        [MaxLength(50)]
        public string Source { get; set; } = "manual"; // manual, datamuse, api

        [MaxLength(10)]
        public string Language { get; set; } = "ru";

        public bool IsActive { get; set; } = true;
    }
}

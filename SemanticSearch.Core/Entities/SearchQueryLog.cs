using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Core.Entities
{
    public class SearchQueryLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string QueryText { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? AlgorithmUsed { get; set; }

        public int ResultCount { get; set; }

        public int ExecutionTimeMs { get; set; }

        [MaxLength(100)]
        public string? UserSessionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? FeedbackScore { get; set; } // 1-5
    }
}

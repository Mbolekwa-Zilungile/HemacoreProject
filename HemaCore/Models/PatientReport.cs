using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemaCore.Models
{
    public class PatientReport
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient Patient { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string ReportType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? ResultId { get; set; }

        public string? FileName { get; set; }

        public byte[]? FileData { get; set; }
    }
}
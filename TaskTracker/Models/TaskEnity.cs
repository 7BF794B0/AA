using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Contracts;

namespace TaskTracker.Models
{
    public class TaskEnity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PublicId { get; }

        [Required]
        public required int UserId { get; set; }

        [Required]
        public required int CreatedBy { get; set; }

        [Required]
        public required string Title { get; set; }

        public string? JiraId { get; set; }

        [Required]
        public required string Description { get; set; }

        [Required]
        public StatusEnum Status { get; set; }

        [Required]
        public required double Estimation { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public required int Cost { get; set; } = 0; // сколько списать денег с сотрудника при ассайне задачи

        [Required]
        public required int Reward { get; set; } = 0; // сколько начислить денег сотруднику для выполненой задачи
    }
}

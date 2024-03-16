using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Billing.Models
{
    public class AccountEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AccountId { get; }

        [Required]
        public required string BillingCycleId { get; set; }

        [Required]
        public required int UserId { get; set; }

        [Required]
        public required int Balance { get; set; } = 0;
    }
}

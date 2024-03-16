using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Billing.Models
{
    public class AccountResponse
    {
        [JsonPropertyName("userId")]
        public required int UserId { get; set; }

        [JsonPropertyName("balance")]
        public required int Balance { get; set; }
    }
}

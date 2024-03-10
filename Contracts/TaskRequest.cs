using System.Text.Json.Serialization;

namespace Contracts
{
    public class TaskRequest
    {
        [JsonPropertyName("userId")]
        public required int UserId { get; set; }

        [JsonPropertyName("createdBy")]
        public required int CreatedBy { get; set; }

        [JsonPropertyName("title")]
        public required string Title { get; set; }

        [JsonPropertyName("description")]
        public required string Description { get; set; }

        [JsonPropertyName("status")]
        public required StatusEnum Status { get; set; }

        [JsonPropertyName("estimation")]
        public required double Estimation { get; set; }

        [JsonPropertyName("createdAt")]
        public required DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

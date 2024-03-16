using System.Text.Json.Serialization;

namespace Contracts
{
    public class UserDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("email")]
        public required string Email { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("role")]
        public required RoleEnum Role { get; set; }

        [JsonPropertyName("balance")]
        public int Balance { get; set; } = 0;
    }
}

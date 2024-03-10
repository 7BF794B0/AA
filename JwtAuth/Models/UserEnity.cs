using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using Contracts;

namespace JwtAuth.Models
{
    public class UserEnity
    {
        static private string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256
            // ComputeHash - returns byte array
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));

            // Convert byte array to a string
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }

        private string _password = string.Empty;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public required string Email { get; set; }

        [Required]
        public required string Password
        {
            get => _password;
            set => _password = ComputeSha256Hash(value);
        }

        [Required]
        public required string Name { get; set; }

        [Required]
        public required RoleEnum Role { get; set; }

        [Required]
        public required int Balance { get; set; } = 0;
    }
}

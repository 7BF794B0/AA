using System.Text.Json.Serialization;

namespace Contracts
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RoleEnum
    {
        Popug,
        Admin,
        Accountant
    }
}

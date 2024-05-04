using System.Text.Json.Serialization;

namespace Contracts
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TransactionTypeEnum
    {
        Income,
        Outcome
    }
}

using System.Text.Json.Serialization;

namespace Contracts
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StatusEnum
    {
        Open,
        InProgress,
        Done,
        ToDo,
        InReview,
        UnderReview,
        Approved,
        Cancelled,
        Rejected
    }
}

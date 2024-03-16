using Contracts;

namespace Billing.Models
{
    public class DoubleEntryBookkeepingRedis
    {
        public required string BillingCycleId { get; set; }

        public required List<DoubleEntryBookkeepingDTO> Records { get; set; } = new List<DoubleEntryBookkeepingDTO>();
    }
}

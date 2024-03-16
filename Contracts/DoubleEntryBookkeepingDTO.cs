namespace Contracts
{
    public class DoubleEntryBookkeepingDTO
    {
        public required int UserId { get; set; }

        public required TransactionTypeEnum TransactionType { get; set; }

        public required int TaskId { get; set; }

        public required int Value { get; set; }
    }
}

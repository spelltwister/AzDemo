namespace MainSystem.Contracts
{
    public interface IProductUpdate
    {
        IQuantityUnits Adjustment { get; }
        string BranchId { get; }
        string CustomerId { get; }
        string ProductId { get; }
        string ReasonCode { get; }
    }
}
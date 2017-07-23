namespace MainSystem.Contracts
{
    public class ProductUpdate : IProductUpdate
    {
        public string CustomerId { get; set; }

        public string BranchId { get; set; }

        public string ProductId { get; set; }

        IQuantityUnits IProductUpdate.Adjustment { get { return this.Adjustment; } }

        public QuantityUnits Adjustment { get; set; }

        public string ReasonCode { get; set; }
    }
}
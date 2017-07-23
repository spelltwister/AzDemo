namespace JoesFoodHouse
{
    public class ProductUpdateMessage
    {
        public string BranchId { get; set; }

        public string ProductId { get; set; }

        public decimal AdjustmentQuantity { get; set; }

        public string AdjustmentUnits { get; set; }

        public string ReasonCode { get; set; }
    }
}
namespace MainSystem.Contracts
{
    public class QuantityUnits : IQuantityUnits
    {
        public decimal Quantity { get; set; }

        public string Units { get; set; }
    }
}
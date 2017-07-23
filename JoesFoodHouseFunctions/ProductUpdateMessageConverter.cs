namespace JoesFoodHouse
{
    public class ProductUpdateMessageConverter
    {
        public static ProductUpdateMessage From(string message)
        {
            var messageParts = message.Split(' ');
            return new ProductUpdateMessage()
            {
                BranchId = messageParts[0],
                ProductId = messageParts[1],
                AdjustmentQuantity = decimal.Parse(messageParts[2]),
                AdjustmentUnits = messageParts[3],
                ReasonCode = messageParts[4]
            };
        }

        public static string To(ProductUpdateMessage message)
        {
            return $"{message.BranchId} {message.ProductId} {message.AdjustmentQuantity.ToString():D2} {message.AdjustmentUnits} {message.ReasonCode}";
        }
    }
}
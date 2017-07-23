namespace MainSystem.Contracts
{
    public class ProductImageUpdate : IProductImageUpdate
    {
        public string ImageUri { get; set; }

        public string ImageType { get; set; }

        public string ProductId { get; set; }

        public string CustomerId { get; set; }
    }
}
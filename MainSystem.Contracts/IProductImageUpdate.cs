namespace MainSystem.Contracts
{
    public interface IProductImageUpdate
    {
        string ImageUri { get; }
        string ImageType { get; }
        string ProductId { get; }
        string CustomerId { get; }
    }
}
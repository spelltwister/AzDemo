using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using MainSystem.Contracts;
using Microsoft.WindowsAzure.Storage.Blob;
using Utility;

namespace JoesFoodHouse
{
    public static class Functions
    {
        private const string UpdateQueueName = "update";
        private const string BatchUpdateQueueName = "batchupdate";
        private const string ImageUpdateQueueName = "productimage";
        private const string OptimizeImageUpdateQueueName = "optproductimage";
        private const string ImagesContainerName = "images";
        private const string PublicImagesContainerName = "publicimages";
        private const string ProductsConnectionName = "jfhConnection";

        private const string MainUpdateQueueName = "productupdate";
        private const string MainImageUpdateQueueName = "productimageupdate";
        private const string MainSystemConnectionName = "mainSystemConnection";

        private static readonly HttpClient DownloadClient = new HttpClient();

        /// <summary>
        /// Customer request to update a single products inventory level
        /// </summary>
        /// <param name="productUpdateMessage">
        /// Custom encoded update message
        /// </param>
        /// <param name="update">
        /// The translated <see cref="ProductUpdate" /> used by the main system
        /// to update product inventory levels
        /// </param>
        /// <param name="log">logger</param>
        [FunctionName("ProductUpdateSingle")]
        public static void ProductUpdateSingle([QueueTrigger(UpdateQueueName, Connection = ProductsConnectionName)]string productUpdateMessage,
            [Queue(MainUpdateQueueName, Connection = MainSystemConnectionName)] out ProductUpdate update,
            TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {productUpdateMessage}");

            var updateMessage = ProductUpdateMessageConverter.From(productUpdateMessage);

            update = new ProductUpdate()
            {
                Adjustment = new QuantityUnits()
                {
                    Quantity = updateMessage.AdjustmentQuantity,
                    Units = updateMessage.AdjustmentUnits
                },
                BranchId = updateMessage.BranchId,
                CustomerId = GetCustomerId(),
                ProductId = updateMessage.ProductId,
                ReasonCode = updateMessage.ReasonCode
            };
        }

        /// <summary>
        /// Customer request to update a batch of product inventory levels
        /// </summary>
        /// <param name="batchUpdate">
        /// Custom encoded batch update messsage
        /// </param>
        /// <param name="singleUpdateCollector">
        /// The <see cref="IAsyncCollector{string}" /> used to collect single
        /// update request messages from the batch
        /// </param>
        /// <param name="log">logger</param>
        [FunctionName("BatchProductUpdate")]
        public static async Task BatchProductUpdateAsync([QueueTrigger(BatchUpdateQueueName, Connection = ProductsConnectionName)]string batchUpdate,
            [Queue(UpdateQueueName, Connection = ProductsConnectionName)] IAsyncCollector<string> singleUpdateCollector,
            TraceWriter log)
        {
            log.Info("Batch updates queue triggered.");

            int count = 0;
            foreach (var batch in batchUpdate.Split(Environment.NewLine.ToCharArray())
                                             .Select(x => singleUpdateCollector.AddAsync(x))
                                             .Chunk(250))
            {
                await Task.WhenAll(batch).ConfigureAwait(false);
                count += batch.Count;
            }

            log.Info($"Enqueued {count} updates.");
        }

        /// <summary>
        /// Customer request to update a products image set
        /// </summary>
        /// <param name="imageUpdateMessage">
        /// Custom encoded image set update message
        /// </param>
        /// <param name="container">
        /// The blob container into which to write the original customer
        /// image set
        /// </param>
        /// <param name="optimizeCollector">
        /// The <see cref="IAsyncCollector{OptimizeUpateImage}" /> used to
        /// collect individual image update tasks.
        /// </param>
        /// <param name="log">logger</param>
        [FunctionName("ProductImageUpdate")]
        public static async Task ProductImageUpdateAsync([QueueTrigger(ImageUpdateQueueName, Connection = ProductsConnectionName)]string imageUpdateMessage,
            [Blob(ImagesContainerName, Connection = ProductsConnectionName)] CloudBlobContainer container,
            [Queue(OptimizeImageUpdateQueueName, Connection = ProductsConnectionName)] IAsyncCollector<OptimizeUpateImage> optimizeCollector,
            TraceWriter log)
        {
            var updateParts = imageUpdateMessage.Split(' ');

            Uri brandImageUri = new Uri(updateParts[0], UriKind.Absolute);
            Uri logoImageUri = new Uri(updateParts[1], UriKind.Absolute);
            Uri productImageUri = new Uri(updateParts[2], UriKind.Absolute);

            string productId = updateParts[3];

            await Task.WhenAll(SendOptimizeAsync(container, brandImageUri, productId, "brand", optimizeCollector),
                               SendOptimizeAsync(container, logoImageUri, productId, "logo", optimizeCollector),
                               SendOptimizeAsync(container, productImageUri, productId, "image", optimizeCollector))
                      .ConfigureAwait(false);
        }

        /// <summary>
        /// Optimizes the given image and sends an image update request to the
        /// main system
        /// </summary>
        /// <param name="optimizeMessage">
        /// The JSON encoded <see cref="OptimizeUpateImage" /> automatically
        /// converted to a DTO
        /// </param>
        /// <param name="container">
        /// The container from which the original image can be read
        /// </param>
        /// <param name="publicContainer">
        /// The container into which the optimized image will be written
        /// and from where it can be read
        /// </param>
        /// <param name="imageUpdateCollector">
        /// The <see cref="IAsyncCollector{ProductImageUpdate}" /> used to
        /// collect product image update requests for the main system
        /// </param>
        /// <param name="log">logger</param>
        [FunctionName("OptimizeAndUpdateImage")]
        public static async Task OptimizeProductImageAsync([QueueTrigger(OptimizeImageUpdateQueueName, Connection = ProductsConnectionName)] OptimizeUpateImage optimizeMessage,
            [Blob(ImagesContainerName, Connection = ProductsConnectionName)] CloudBlobContainer container,
            [Blob(PublicImagesContainerName, Connection = ProductsConnectionName)] CloudBlobContainer publicContainer,
            // can't use out parameter, so use collector
            [Queue(MainImageUpdateQueueName, Connection = MainSystemConnectionName)] IAsyncCollector<ProductImageUpdate> imageUpdateCollector,
            TraceWriter log)
        {
            var blob = container.GetBlockBlobReference(optimizeMessage.BlobName);
            if(!await blob.ExistsAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Specified blob does not exist! {optimizeMessage.BlobName}");
            }

            byte[] originalBytes = new byte[blob.Properties.Length];
            await blob.DownloadToByteArrayAsync(originalBytes, 0).ConfigureAwait(false);

            byte[] optimizedBytes = await OptimizeImageBytesAsync(originalBytes).ConfigureAwait(false);

            var publicBlob = publicContainer.GetBlockBlobReference(optimizeMessage.BlobName);
            await publicBlob.UploadFromByteArrayAsync(optimizedBytes, 0, optimizedBytes.Length);

            await imageUpdateCollector.AddAsync(new ProductImageUpdate()
            {
                CustomerId = GetCustomerId(),
                ImageType = optimizeMessage.ImageType,
                ImageUri = publicBlob.Uri.AbsoluteUri,
                ProductId = optimizeMessage.ProductId
            }).ConfigureAwait(false);
        }

        private static async Task<byte[]> OptimizeImageBytesAsync(byte[] imageToOptimize)
        {
            // TODO: actually optimize the image
            return imageToOptimize;
        }

        private static async Task SendOptimizeAsync(CloudBlobContainer container, Uri imageUri, string productId, string imageType, IAsyncCollector<OptimizeUpateImage> optimizeCollector)
        {
            var setup = await SetupOptimizeAsync(container, imageUri, productId, imageType).ConfigureAwait(false);
            await optimizeCollector.AddAsync(setup).ConfigureAwait(false);
        }

        private static async Task<OptimizeUpateImage> SetupOptimizeAsync(CloudBlobContainer container, Uri imageUri, string productId, string imageType)
        {
            var blob = await DownloadImageLocalAsync(container, imageUri).ConfigureAwait(false);
            return new OptimizeUpateImage()
            {
                BlobName = blob.Name,
                ImageType = imageType,
                ProductId = productId
            };
        }

        private static async Task<CloudBlockBlob> DownloadImageLocalAsync(CloudBlobContainer container, Uri imageUri)
        {
            var bytes = await DownloadClient.GetByteArrayAsync(imageUri).ConfigureAwait(false);

            string randomImageBlobName = Guid.NewGuid().ToString("N");
            var blob = container.GetBlockBlobReference(randomImageBlobName);
            await blob.UploadFromByteArrayAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            return blob;
        }

        public static string GetCustomerId()
        {
            // TODO: pull from database or configuration
            return "JoesFoodHouse";
        }
    }
}
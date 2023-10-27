using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DirectoryManager.FileStorage.BaseClasses;
using DirectoryManager.FileStorage.Constants;
using DirectoryManager.FileStorage.Repositories.Interfaces;

namespace DirectoryManager.FileStorage.Repositories.Implementations
{
    public class BlobService : BaseBlobFiles, IBlobService
    {
        private BlobService(BlobServiceClient blobServiceClient)
        {
            this.BlobServiceClient = blobServiceClient;
        }

        public static async Task<BlobService> CreateAsync(BlobServiceClient blobServiceClient)
        {
            var blobService = new BlobService(blobServiceClient);
            if (blobService.BlobServiceClient != null)
            {
                var container = blobService.BlobServiceClient.GetBlobContainerClient(StringConstants.ContainerName);
                await blobService.CreateIfNotExistsAsync(container);
                await blobService.SetCorsAsync(blobServiceClient);
            }

            return blobService;
        }

        public BlobServiceClient BlobServiceClient { get; private set; }

        public BlobContainerClient GetContainerReference(string containerName)
        {
            return this.BlobServiceClient?.GetBlobContainerClient(containerName);
        }

        private async Task CreateIfNotExistsAsync(BlobContainerClient container)
        {
            if (!await container.ExistsAsync())
            {
                var publicAccessType = PublicAccessType.Blob;
                await container.CreateIfNotExistsAsync(publicAccessType);
            }
        }
    }
}
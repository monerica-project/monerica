using Azure.Storage.Blobs;

namespace DirectoryManager.FileStorage.Repositories.Interfaces
{
    public interface IBlobService
    {
        string BlobPrefix { get; }

        BlobContainerClient? GetContainerReference(string containerName);
    }
}
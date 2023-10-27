using Azure.Storage.Blobs;

namespace DirectoryManager.FileStorage.Repositories.Interfaces
{
    public interface IBlobService
    {
        BlobContainerClient? GetContainerReference(string containerName);
    }
}
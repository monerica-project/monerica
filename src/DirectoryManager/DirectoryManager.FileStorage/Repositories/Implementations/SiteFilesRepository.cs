using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DirectoryManager.FileStorage.BaseClasses;
using DirectoryManager.FileStorage.Constants;
using DirectoryManager.FileStorage.Models;
using DirectoryManager.FileStorage.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.FileStorage.Repositories.Implementations
{
    public class SiteFilesRepository : BaseBlobFiles, ISiteFilesRepository
    {
        private readonly IBlobService blobService;

        public SiteFilesRepository(IBlobService blobService)
        {
            this.blobService = blobService;
        }

        public string BlobPrefix
        {
            get
            {
                return this.blobService.BlobPrefix;
            }
        }

        public async Task<SiteFileDirectory> ListFilesAsync(string? prefix = null)
        {
            var directory = new SiteFileDirectory();
            var container = this.blobService.GetContainerReference(StringConstants.ContainerName);

            if (prefix != null && prefix.StartsWith("/"))
            {
                prefix = prefix.Remove(0, 1);
            }

            if (container == null)
            {
                return directory;
            }

            await foreach (var page in container.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/").AsPages())
            {
                foreach (var item in page.Values)
                {
                    if (item.IsBlob)
                    {
                        directory.FileItems.Add(new SiteFileItem
                        {
                            FilePath = $"{container.Uri}/{item.Blob.Name}",
                            IsFolder = false
                        });
                    }
                    else if (item.IsPrefix)
                    {
                        this.AddDirectory(directory, item);
                    }
                }
            }

            return directory;
        }

        public async Task DeleteFileAsync(string blobPath)
        {
            if (string.IsNullOrWhiteSpace(blobPath))
            {
                return;
            }

            try
            {
                var container = this.blobService.GetContainerReference(StringConstants.ContainerName);

                if (container == null)
                {
                    return;
                }

                var filePrefix = this.BlobPrefix.TrimEnd('/') + "/" + container.Name.TrimEnd('/') + "/";
                var fileName = blobPath.StartsWith(filePrefix) ? blobPath[filePrefix.Length..] : blobPath;
                var blob = container.GetBlobClient(fileName);

                await blob.DeleteIfExistsAsync();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Blob not found, no action needed
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }

        public async Task DeleteFolderAsync(string? folderPath)
        {
            var allInDir = await this.GetDirContentsAsync(folderPath);

            foreach (var item in allInDir)
            {
                // todo: this was path, check how to do this now
                await this.DeleteFileAsync(item.Name);
            }
        }

        public async Task<Uri> UploadAsync(Stream? stream, string? fileName, string? directory = null)
        {
            try
            {
                if (stream == null || string.IsNullOrWhiteSpace(fileName))
                {
                    throw new Exception("Stream or file name is null");
                }

                fileName = FileNameUtilities.RemoveSpacesInFileName(fileName);

                if (fileName == StringConstants.FolderFileName)
                {
                    throw new Exception($"File name cannot be {StringConstants.FolderFileName}");
                }

                var filePath = fileName;

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    filePath = directory + filePath;

                    if (filePath.StartsWith("/"))
                    {
                        filePath = filePath.Remove(0, 1);
                    }
                }

                var container =
                    this.blobService.GetContainerReference(StringConstants.ContainerName)
                    ?? throw new Exception("Container not found");

                if (container == null)
                {
                    throw new Exception("Container not found");
                }

                var blob = container.GetBlobClient(filePath);

                stream.Seek(0, SeekOrigin.Begin);

                await blob.UploadAsync(stream, overwrite: true);
                var extension = FileNameUtilities.GetFileExtensionLower(fileName);

                // Assuming SetPropertiesAsync is updated for the new SDK as well
                await this.SetPropertiesAsync(blob, extension);

                return blob.Uri;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }

        public async Task CreateFolderAsync(string? folderPath, string? directory = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    return;
                }

                folderPath = folderPath.Replace("/", string.Empty);

                var container = this.blobService.GetContainerReference(StringConstants.ContainerName);
                var path = $"{folderPath}/{StringConstants.FolderFileName}";

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    path = $"{directory}{path}";
                    if (path.StartsWith("/"))
                    {
                        path = path.Remove(0, 1);
                    }
                }

                if (container == null)
                {
                    return;
                }

                var blockBlob = container.GetBlobClient(path);

                if (await blockBlob.ExistsAsync())
                {
                    return;
                }

                using var memoryStream = new MemoryStream();
                var tw = new StreamWriter(memoryStream);
                tw.WriteLine(folderPath);
                tw.Flush();
                memoryStream.Seek(0, SeekOrigin.Begin);

                await blockBlob.UploadAsync(memoryStream, overwrite: true);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }

        public async Task ChangeFileName(string? currentFileName, string? newFileName)
        {
            if (string.IsNullOrWhiteSpace(currentFileName) || string.IsNullOrWhiteSpace(newFileName))
            {
                return;
            }

            var container = this.blobService.GetContainerReference(StringConstants.ContainerName);

            if (container == null)
            {
                return;
            }

            await container.CreateIfNotExistsAsync();
            BlobClient blobCopy = container.GetBlobClient(newFileName);
            if (!await blobCopy.ExistsAsync())
            {
                BlobClient blob = container.GetBlobClient(currentFileName);

                if (await blob.ExistsAsync())
                {
                    await blobCopy.StartCopyFromUriAsync(blob.Uri);
                    await blob.DeleteIfExistsAsync();
                }
            }
        }

        private async Task<List<BlobItem>> GetDirContentsAsync(string? prefix = null)
        {
            var container = this.blobService.GetContainerReference(StringConstants.ContainerName);

            if (prefix != null && prefix.StartsWith("/"))
            {
                prefix = prefix.Remove(0, 1);
            }

            if (container == null)
            {
                return new List<BlobItem>();
            }

            var resultSegment = container.GetBlobsAsync(prefix: prefix);
            var allInDir = new List<BlobItem>();
            await foreach (var blobItem in resultSegment)
            {
                allInDir.Add(blobItem);
            }

            return allInDir;
        }

        private void AddDirectory(SiteFileDirectory directory, BlobHierarchyItem blobDirectory)
        {
            var folderName = blobDirectory.Prefix.Split('/')[^2];
            var pathFromRoot = new Uri(this.BlobPrefix + blobDirectory.Prefix)
                                    .LocalPath
                                    .Replace(string.Format("/{0}", StringConstants.ContainerName), string.Empty);

            directory.FileItems.Add(new SiteFileItem
            {
                FilePath = blobDirectory.Prefix,
                IsFolder = true,
                FolderName = folderName,
                FolderPathFromRoot = pathFromRoot
            });
        }
    }
}
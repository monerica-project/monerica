using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using DirectoryManager.FileStorage.Constants;
using DirectoryManager.FileStorage.Models;
using DirectoryManager.FileStorage.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.FileStorage.Repositories.Implementations
{
    /// <summary>
    /// Bunny.net Edge Storage implementation of <see cref="ISiteFilesRepository"/>.
    /// Files live under the same "directorycontent/&lt;path&gt;" prefix they had in Azure,
    /// so cdn.monerica.com URLs are byte-for-byte unchanged after the pull-zone origin
    /// is repointed at this storage zone. BlobPrefix is the storage HTTP base, and
    /// <c>UrlBuilder.ConvertBlobToCdnUrl</c> swaps it for the CDN prefix exactly as before.
    /// </summary>
    public class BunnySiteFilesRepository : ISiteFilesRepository
    {
        private const string Container = StringConstants.ContainerName; // "directorycontent"

        private readonly HttpClient http;
        private readonly string zone;
        private readonly string storageHost;

        public BunnySiteFilesRepository(string storageZoneName, string accessKey, string? storageHostname)
        {
            if (string.IsNullOrWhiteSpace(storageZoneName))
            {
                throw new ArgumentException("Bunny storage zone name is required.", nameof(storageZoneName));
            }

            if (string.IsNullOrWhiteSpace(accessKey))
            {
                throw new ArgumentException("Bunny storage access key is required.", nameof(accessKey));
            }

            this.zone = storageZoneName;
            this.storageHost = string.IsNullOrWhiteSpace(storageHostname) ? "storage.bunnycdn.com" : storageHostname;
            this.http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            this.http.DefaultRequestHeaders.Add("AccessKey", accessKey);
        }

        // Trailing slash so ConvertBlobToCdnUrl(url, BlobPrefix, cdnPrefix) yields
        // "https://cdn.monerica.com/directorycontent/<path>".
        public string BlobPrefix => $"https://{this.storageHost}/{this.zone}/";

        public async Task<Uri> UploadAsync(Stream? stream, string? fileName, string? directory = null)
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

            stream.Seek(0, SeekOrigin.Begin);
            var url = this.ObjectUrl(filePath);
            using var content = new StreamContent(stream);
            var resp = await this.http.PutAsync(url, content);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                throw new Exception($"Bunny upload failed ({(int)resp.StatusCode}): {body}");
            }

            return new Uri(url);
        }

        public async Task DeleteFileAsync(string blobPath)
        {
            if (string.IsNullOrWhiteSpace(blobPath))
            {
                return;
            }

            var relative = this.ToContainerRelative(blobPath);
            if (string.IsNullOrEmpty(relative))
            {
                return;
            }

            var resp = await this.http.DeleteAsync(this.ObjectUrl(relative));
            if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.NotFound)
            {
                throw new Exception($"Bunny delete failed ({(int)resp.StatusCode}) for {relative}");
            }
        }

        public async Task<SiteFileDirectory> ListFilesAsync(string? prefix = null)
        {
            var directory = new SiteFileDirectory();
            var rel = NormalizePrefix(prefix);

            foreach (var item in await this.ListAsync(rel))
            {
                if (item.IsDirectory)
                {
                    var folderName = item.ObjectName ?? string.Empty;
                    directory.FileItems.Add(new SiteFileItem
                    {
                        FilePath = rel + folderName + "/",
                        IsFolder = true,
                        FolderName = folderName,
                        FolderPathFromRoot = "/" + rel + folderName + "/",
                    });
                }
                else
                {
                    directory.FileItems.Add(new SiteFileItem
                    {
                        FilePath = this.ObjectUrl(rel + item.ObjectName),
                        IsFolder = false,
                    });
                }
            }

            return directory;
        }

        public async Task<IReadOnlyList<string>> ListFolderBlobNamesAsync(string? folderPath)
        {
            var rel = NormalizePrefix(this.ToContainerRelative(folderPath ?? string.Empty));
            var names = new List<string>();
            foreach (var item in await this.ListAsync(rel))
            {
                if (!item.IsDirectory)
                {
                    names.Add(rel + item.ObjectName);
                }
            }

            return names;
        }

        public async Task DeleteFolderAsync(string? folderPath)
        {
            var rel = NormalizePrefix(this.ToContainerRelative(folderPath ?? string.Empty));
            if (string.IsNullOrEmpty(rel))
            {
                return;
            }

            // Bunny deletes a directory (path ending with '/') recursively.
            var resp = await this.http.DeleteAsync(this.ObjectUrl(rel));
            if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.NotFound)
            {
                throw new Exception($"Bunny delete-folder failed ({(int)resp.StatusCode}) for {rel}");
            }
        }

        public async Task CreateFolderAsync(string? folderPath, string? directory = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            folderPath = folderPath.Replace("/", string.Empty);
            var path = $"{folderPath}/{StringConstants.FolderFileName}";
            if (!string.IsNullOrWhiteSpace(directory))
            {
                path = $"{directory}{path}";
                if (path.StartsWith("/"))
                {
                    path = path.Remove(0, 1);
                }
            }

            using var marker = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(folderPath + "\n"));
            using var content = new StreamContent(marker);
            var resp = await this.http.PutAsync(this.ObjectUrl(path), content);
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Bunny create-folder failed ({(int)resp.StatusCode}) for {path}");
            }
        }

        public async Task ChangeFileName(string? currentFileName, string? newFileName)
        {
            if (string.IsNullOrWhiteSpace(currentFileName) || string.IsNullOrWhiteSpace(newFileName))
            {
                return;
            }

            var from = this.ToContainerRelative(currentFileName);
            var to = this.ToContainerRelative(newFileName);

            var get = await this.http.GetAsync(this.ObjectUrl(from));
            if (!get.IsSuccessStatusCode)
            {
                return;
            }

            var bytes = await get.Content.ReadAsByteArrayAsync();
            using var content = new ByteArrayContent(bytes);
            var put = await this.http.PutAsync(this.ObjectUrl(to), content);
            if (put.IsSuccessStatusCode)
            {
                await this.DeleteFileAsync(from);
            }
        }

        private static string NormalizePrefix(string? prefix)
        {
            var rel = (prefix ?? string.Empty).TrimStart('/');
            if (rel.Length > 0 && !rel.EndsWith("/"))
            {
                rel += "/";
            }

            return rel;
        }

        // Storage URL for a path inside the directorycontent container.
        private string ObjectUrl(string relPath) => $"{this.BlobPrefix}{Container}/{relPath.TrimStart('/')}";

        // Reduce any of {full storage URL, CDN URL, "/directorycontent/x", "directorycontent/x",
        // "reviews/1/x"} to the path relative to the container root ("reviews/1/x").
        private string ToContainerRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var full = this.BlobPrefix + Container + "/";
            if (path.StartsWith(full, StringComparison.OrdinalIgnoreCase))
            {
                return path[full.Length..].TrimStart('/');
            }

            var marker = "/" + Container + "/";
            var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return path[(idx + marker.Length)..].TrimStart('/');
            }

            if (path.StartsWith(Container + "/", StringComparison.OrdinalIgnoreCase))
            {
                return path[(Container.Length + 1)..].TrimStart('/');
            }

            return path.TrimStart('/');
        }

        private async Task<List<BunnyObject>> ListAsync(string relPrefix)
        {
            var url = $"{this.BlobPrefix}{Container}/{relPrefix}";
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            var resp = await this.http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                return new List<BunnyObject>();
            }

            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<BunnyObject>>(json) ?? new List<BunnyObject>();
        }

        private sealed class BunnyObject
        {
            [JsonPropertyName("ObjectName")]
            public string? ObjectName { get; set; }

            [JsonPropertyName("IsDirectory")]
            public bool IsDirectory { get; set; }

            [JsonPropertyName("Length")]
            public long Length { get; set; }
        }
    }
}

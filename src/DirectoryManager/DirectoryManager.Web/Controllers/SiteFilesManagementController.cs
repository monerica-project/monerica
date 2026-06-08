using DirectoryManager.Data.Enums;
using DirectoryManager.FileStorage.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class SiteFilesManagementController : Controller
    {
        private readonly ISiteFilesRepository siteFilesRepository;
        private readonly ICacheService cacheService;

        public SiteFilesManagementController(
            ISiteFilesRepository siteFilesRepository,
            ICacheService cacheService)
        {
            this.siteFilesRepository = siteFilesRepository;
            this.cacheService = cacheService;
        }

        [Route("sitefilesmanagement/upload")]
        [HttpGet]
        public ActionResult Upload(string? folderPath = null)
        {
            this.ViewBag.UploadFolderPath = folderPath;

            return this.View();
        }

        [Route("sitefilesmanagement/uploadasync")]
        [HttpPost]
        public async Task<ActionResult> UploadAsync(IEnumerable<IFormFile> files, string? folderPath = null)
        {
            try
            {
                // Reject a traversal/absolute/control-char folder path outright.
                if (!IsSafeFolderPath(folderPath))
                {
                    return this.RedirectToAction("Index");
                }

                foreach (var file in files)
                {
                    if (file == null || file.Length <= 0)
                    {
                        continue;
                    }

                    // Size cap.
                    if (file.Length > MaxUploadBytes)
                    {
                        continue;
                    }

                    // Strip any directory portion the client may have sent, then validate.
                    var safeName = Path.GetFileName(file.FileName ?? string.Empty);
                    if (!IsSafeFileName(safeName))
                    {
                        continue;
                    }

                    // Extension allowlist.
                    var ext = Path.GetExtension(safeName);
                    if (string.IsNullOrEmpty(ext) || !AllowedUploadExtensions.Contains(ext))
                    {
                        continue;
                    }

                    using var stream = file.OpenReadStream();
                    await this.siteFilesRepository.UploadAsync(stream, safeName, folderPath);
                }

                return this.RedirectToAction("Index");
            }
            catch (Exception)
            {
                return this.RedirectToAction("Index");
            }
        }

        // 25 MB ceiling per file.
        private const long MaxUploadBytes = 25 * 1024 * 1024;

        // Extensions an admin may upload. Intentionally narrow; widen here if a real asset
        // type is missing. NOTE: blobs are served from a separate storage/CDN origin and the
        // app enforces script-src 'none', so .svg is low-risk here, but drop it from the list
        // if you never serve admin-uploaded SVGs.
        private static readonly HashSet<string> AllowedUploadExtensions =
            new (StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".svg",
                ".pdf", ".txt", ".csv", ".json", ".xml",
                ".css", ".webmanifest",
                ".woff", ".woff2", ".ttf", ".otf", ".eot",
            };

        private static bool IsSafeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (name.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            if (name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0)
            {
                return false;
            }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            return true;
        }

        private static bool IsSafeFolderPath(string? folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return true; // null/empty == root, allowed
            }

            if (folderPath.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            if (folderPath.Contains('\\'))
            {
                return false;
            }

            if (folderPath.StartsWith('/'))
            {
                return false; // no rooted/absolute paths; '/' is only an inner separator
            }

            foreach (var ch in folderPath)
            {
                if (char.IsControl(ch))
                {
                    return false;
                }
            }

            return true;
        }

        [Route("sitefilesmanagement/CreateFolderAsync")]
        [HttpPost]
        public async Task<ActionResult> CreateFolderAsync(string folderName, string? currentDirectory = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(folderName))
                {
                    folderName = folderName.Trim();

                    await this.siteFilesRepository.CreateFolderAsync(folderName, currentDirectory);
                }

                return this.RedirectToAction("Index");
            }
            catch (Exception)
            {
                return this.RedirectToAction("Index");
            }
        }

        [Route("sitefilesmanagement")]
        [HttpGet]
        public async Task<ActionResult> IndexAsync(string? folderPath = null)
        {
            var directory = await this.siteFilesRepository.ListFilesAsync(folderPath);

            var model = new SiteFileListModel();

            foreach (var file in directory.FileItems)
            {
                var item = new SiteFileItem
                {
                    FilePath = file.FilePath ?? string.Empty,
                    FolderName = file.FolderName ?? string.Empty,
                    FolderPathFromRoot = file.FolderPathFromRoot ?? string.Empty,
                    IsFolder = file.IsFolder
                };

                if (!file.IsFolder)
                {
                    if (string.IsNullOrWhiteSpace(file.FilePath))
                    {
                        throw new ArgumentNullException($"{nameof(file.FilePath)}");
                    }

                    item.CdnLink = await this.ConvertBlobToCdnUrlAsync(file.FilePath);
                }

                model.FileItems.Add(item);
            }

            var folders = model.FileItems.Where(x => x.IsFolder == true).OrderBy(x => x.FolderName).ToList();
            var files = model.FileItems.Where(x => x.IsFolder == false).OrderBy(x => x.FilePath).ToList();

            model.FileItems = new List<SiteFileItem>();
            model.FileItems.AddRange(folders);
            model.FileItems.AddRange(files);

            if (folderPath != null)
            {
                model.CurrentDirectory = folderPath;
                var lastPath = folderPath.Split('/')[^2];

                if (string.IsNullOrWhiteSpace(lastPath))
                {
                    model.ParentDirectory = string.Empty;
                }
                else
                {
                    var lastPart = lastPath + "/";
                    var startIndex = folderPath.IndexOf(lastPart);
                    model.ParentDirectory = folderPath.Remove(startIndex, lastPart.Length);
                }
            }

            return this.View(model);
        }

        [Route("sitefilesmanagement/DeleteFileAsync")]
        [HttpGet]
        public async Task<ActionResult> DeleteFileAsync(string fileUrl)
        {
            await this.siteFilesRepository.DeleteFileAsync(fileUrl);

            return this.RedirectToAction("Index");
        }

        [Route("sitefilesmanagement/DeleteFolderAsync")]
        [HttpGet]
        public async Task<ActionResult> DeleteFolderAsync(string folderUrl)
        {
            await this.siteFilesRepository.DeleteFolderAsync(folderUrl);

            return this.RedirectToAction("Index");
        }

        private async Task<string> ConvertBlobToCdnUrlAsync(string filePath)
        {
            var cdnPrefix = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CdnPrefixWithProtocol);

            return UrlBuilder.ConvertBlobToCdnUrl(filePath, this.siteFilesRepository.BlobPrefix, cdnPrefix);
        }
    }
}
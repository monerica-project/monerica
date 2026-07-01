using DirectoryManager.Data.Enums;
using DirectoryManager.FileStorage.Constants;
using DirectoryManager.FileStorage.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class SiteFilesManagementController : Controller
    {
        // 25 MB ceiling per file.
        private const long MaxUploadBytes = 25 * 1024 * 1024;

        // Extensions an admin may upload. Intentionally narrow; widen here if a real asset
        // type is missing. NOTE: blobs are served from a separate storage/CDN origin and the
        // app enforces script-src 'none', so .svg is low-risk here, but drop it from the list
        // if you never serve admin-uploaded SVGs.
        private static readonly HashSet<string> AllowedUploadExtensions =
            new (StringComparer.OrdinalIgnoreCase)
            {
                // NOTE: .svg and .xml are intentionally NOT allowed — served from this
                // origin they can carry <script>/markup and execute (stored XSS).
                ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".bmp", ".ico",
                ".pdf", ".txt", ".csv", ".json",
                ".css", ".webmanifest",
                ".woff", ".woff2", ".ttf", ".otf", ".eot",
            };

        private readonly ISiteFilesRepository siteFilesRepository;
        private readonly ICacheService cacheService;
        private readonly ILogger<SiteFilesManagementController> logger;

        public SiteFilesManagementController(
            ISiteFilesRepository siteFilesRepository,
            ICacheService cacheService,
            ILogger<SiteFilesManagementController> logger)
        {
            this.siteFilesRepository = siteFilesRepository;
            this.cacheService = cacheService;
            this.logger = logger;
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
                // Folder navigation produces rooted paths like "/exchange-reviews/".
                // Normalize to blob-relative before validating; the repository expects no leading slash.
                folderPath = folderPath?.TrimStart('/');

                // Reject a traversal/absolute/control-char folder path outright.
                if (!IsSafeFolderPath(folderPath))
                {
                    this.TempData["Error"] = "Upload rejected: unsafe folder path.";
                    return this.RedirectToAction("Index", new { folderPath });
                }

                var fileList = files?.ToList() ?? new List<IFormFile>();
                if (fileList.Count == 0)
                {
                    this.TempData["Error"] = "No file was received. Pick a file before submitting.";
                    return this.RedirectToAction("Index", new { folderPath });
                }

                var uploaded = 0;
                var skipped = new List<string>();

                foreach (var file in fileList)
                {
                    if (file == null || file.Length <= 0)
                    {
                        skipped.Add($"{file?.FileName ?? "(unnamed)"} (empty)");
                        continue;
                    }

                    if (file.Length > MaxUploadBytes)
                    {
                        skipped.Add($"{file.FileName} (over 25 MB)");
                        continue;
                    }

                    // Strip any directory portion the client may have sent, then validate.
                    var safeName = Path.GetFileName(file.FileName ?? string.Empty);
                    if (!IsSafeFileName(safeName))
                    {
                        skipped.Add($"{file.FileName} (invalid file name)");
                        continue;
                    }

                    // Extension allowlist.
                    var ext = Path.GetExtension(safeName);
                    if (string.IsNullOrEmpty(ext) || !AllowedUploadExtensions.Contains(ext))
                    {
                        skipped.Add($"{safeName} (extension '{ext}' not allowed)");
                        continue;
                    }

                    using var stream = file.OpenReadStream();
                    await this.siteFilesRepository.UploadAsync(stream, safeName, folderPath);
                    uploaded++;
                }

                if (uploaded > 0)
                {
                    this.TempData["Status"] = $"Uploaded {uploaded} file(s).";
                }

                if (skipped.Count > 0)
                {
                    this.TempData["Error"] = "Skipped: " + string.Join("; ", skipped);
                }

                return this.RedirectToAction("Index", new { folderPath });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "File upload failed for folder '{FolderPath}'.", folderPath);
                this.TempData["Error"] = "Upload failed: " + ex.Message;
                return this.RedirectToAction("Index", new { folderPath });
            }
        }

        [Route("sitefilesmanagement/diag")]
        [HttpGet]
        public async Task<IActionResult> Diag()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("BlobPrefix: " + this.siteFilesRepository.BlobPrefix);

            try
            {
                var listing = await this.siteFilesRepository.ListFilesAsync(null);
                sb.AppendLine($"LIST OK — {listing.FileItems.Count} item(s)");
            }
            catch (Exception ex)
            {
                sb.AppendLine("LIST FAILED:\n" + ex);
                return this.Content(sb.ToString(), "text/plain");
            }

            try
            {
                await this.siteFilesRepository.CreateFolderAsync("diag-test-folder", null);
                sb.AppendLine("CREATE FOLDER OK — check the file list");
            }
            catch (Exception ex)
            {
                sb.AppendLine("CREATE FAILED:\n" + ex);
            }

            return this.Content(sb.ToString(), "text/plain");
        }

        [Route("sitefilesmanagement/CreateFolderAsync")]
        [HttpPost]
        public async Task<ActionResult> CreateFolderAsync(string folderName, string? currentDirectory = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderName))
                {
                    this.TempData["Error"] = "Folder name was empty.";
                    return this.RedirectToAction("Index", new { folderPath = currentDirectory });
                }

                folderName = folderName.Trim();

                await this.siteFilesRepository.CreateFolderAsync(folderName, currentDirectory);

                this.TempData["Status"] = $"Created folder '{folderName}'.";
                return this.RedirectToAction("Index", new { folderPath = currentDirectory });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Create folder '{FolderName}' failed in '{CurrentDirectory}'.", folderName, currentDirectory);
                this.TempData["Error"] = "Create folder failed: " + ex.Message;
                return this.RedirectToAction("Index", new { folderPath = currentDirectory });
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteFileAsync(string fileUrl)
        {
            await this.siteFilesRepository.DeleteFileAsync(fileUrl);

            return this.RedirectToAction("Index");
        }

        [Route("sitefilesmanagement/ConfirmDeleteFolder")]
        [HttpGet]
        public async Task<ActionResult> ConfirmDeleteFolderAsync(string folderUrl)
        {
            // FolderPathFromRoot arrives rooted, e.g. "/exchange-reviews/".
            // Normalize to blob-relative before validating; mirror the upload path handling.
            var normalized = folderUrl?.TrimStart('/');

            if (string.IsNullOrWhiteSpace(folderUrl) || !IsSafeFolderPath(normalized))
            {
                this.TempData["Error"] = "Delete rejected: unsafe or missing folder path.";
                return this.RedirectToAction("Index");
            }

            var names = await this.siteFilesRepository.ListFolderBlobNamesAsync(folderUrl);

            var prefix = normalized!.TrimEnd('/') + "/";
            var realFiles = new List<string>();
            var markerCount = 0;

            foreach (var name in names)
            {
                var fileName = Path.GetFileName(name);

                if (string.Equals(fileName, StringConstants.FolderFileName, StringComparison.OrdinalIgnoreCase))
                {
                    markerCount++;
                    continue;
                }

                var display = name.StartsWith(prefix, StringComparison.Ordinal)
                    ? name[prefix.Length..]
                    : name;

                realFiles.Add(display);
            }

            realFiles.Sort(StringComparer.OrdinalIgnoreCase);

            var trimmed = normalized.TrimEnd('/');

            var model = new ConfirmDeleteFolderModel
            {
                FolderUrl = folderUrl,
                FolderName = string.IsNullOrEmpty(trimmed) ? folderUrl : trimmed.Split('/')[^1],
                Files = realFiles,
                MarkerFileCount = markerCount
            };

            return this.View("ConfirmDeleteFolder", model);
        }

        [Route("sitefilesmanagement/DeleteFolderAsync")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteFolderAsync(string folderUrl)
        {
            var normalized = folderUrl?.TrimStart('/');

            if (string.IsNullOrWhiteSpace(folderUrl) || !IsSafeFolderPath(normalized))
            {
                this.TempData["Error"] = "Delete rejected: unsafe or missing folder path.";
                return this.RedirectToAction("Index");
            }

            await this.siteFilesRepository.DeleteFolderAsync(folderUrl);

            this.TempData["Status"] = $"Deleted folder '{folderUrl}' and its contents.";
            return this.RedirectToAction("Index");
        }

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

        private async Task<string> ConvertBlobToCdnUrlAsync(string filePath)
        {
            var cdnPrefix = await this.cacheService.GetSnippetAsync(SiteConfigSetting.CdnPrefixWithProtocol);

            return UrlBuilder.ConvertBlobToCdnUrl(filePath, this.siteFilesRepository.BlobPrefix, cdnPrefix);
        }
    }
}
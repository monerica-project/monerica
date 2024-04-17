using DirectoryManager.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class FaviconController : Controller
    {
        private const string FavIconPath = "/sitecontent/icons/";
        private const string FavIconAppleIcon57x57 = "apple-icon-57x57.png";
        private const string FavIconAppleIcon60x60 = "apple-icon-60x60.png";
        private const string FavIconAppleIcon72x72 = "apple-icon-72x72.png";
        private const string FavIconAppleIcon76x76 = "apple-icon-76x76.png";
        private const string FavIconAppleIcon114x114 = "apple-icon-114x114.png";
        private const string FavIconAppleIcon120x120 = "apple-icon-120x120.png";
        private const string FavIconAppleIcon144x144 = "apple-icon-144x144.png";
        private const string FavIconAppleIcon152x152 = "apple-icon-152x152.png";
        private const string FavIconAppleIcon180x180 = "apple-icon-180x180.png";
        private const string FavIconAndroid192x192 = "android-icon-192x192.png";
        private const string FavIcon32x32 = "favicon-32x32.png";
        private const string FavIcon96x96 = "favicon-96x96.png";
        private const string FavIcon16x16 = "favicon-16x16.png";
        private const string FavIconMs144x144 = "ms-icon-144x144.png";
        private const string FavIconMaifestJson = "manifest.json";
        private const string FavIconIco = "favicon.ico";

        public FaviconController()
        {
        }

        [Route(FavIconAppleIcon57x57)]
        [Route(FavIconAppleIcon60x60)]
        [Route(FavIconAppleIcon72x72)]
        [Route(FavIconAppleIcon76x76)]
        [Route(FavIconAppleIcon114x114)]
        [Route(FavIconAppleIcon120x120)]
        [Route(FavIconAppleIcon144x144)]
        [Route(FavIconAppleIcon152x152)]
        [Route(FavIconAppleIcon180x180)]
        [Route(FavIconAndroid192x192)]
        [Route(FavIcon32x32)]
        [Route(FavIcon96x96)]
        [Route(FavIcon16x16)]
        [Route(FavIconMs144x144)]
        [Route(FavIconMs144x144)]
        [Route(FavIconMaifestJson)]
        [Route(FavIconIco)]
        [HttpGet]
        public IActionResult FavIcon()
        {
            var pathValue = this.Request.Path.Value;

            if (string.IsNullOrEmpty(pathValue))
            {
                return this.BadRequest("Path value is missing.");
            }

            return this.ReturnContent(pathValue);
        }

        private IActionResult ReturnContent(string fileName)
        {
            fileName = fileName.TrimStart('/');

            try
            {
                var ms = new MemoryStream();

                switch (fileName.GetFileExtensionLower())
                {
                    case "json":
                        return this.File(ms, "application/json");
                    case "ico":
                        return this.File(ms, "image/x-icon");
                    default:
                        return this.File(ms, "text/plain");
                }
            }
            catch
            {
                return this.StatusCode(404);
            }
        }
    }
}

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using DirectoryManager.FileStorage.Constants;

namespace DirectoryManager.FileStorage.BaseClasses
{
    public class BaseBlobFiles
    {
        protected async Task SetPropertiesAsync(BlobClient blockBlob, string extension)
        {
            BlobHttpHeaders blobHttpHeaders = new ();

            switch (extension.ToLower())
            {
                // images
                case "png":
                    blobHttpHeaders.ContentType = "image/png";
                    break;
                case "jpeg":
                case "jpg":
                    blobHttpHeaders.ContentType = "image/jpeg";
                    break;
                case "gif":
                    blobHttpHeaders.ContentType = "image/gif";
                    break;
                case "svg":
                    blobHttpHeaders.ContentType = "image/svg+xml";
                    break;
                case "ico":
                    blobHttpHeaders.ContentType = "image/x-icon";
                    break;
                case "webp":
                    blobHttpHeaders.ContentType = "image/webp";
                    break;

                // style
                case "css":
                    blobHttpHeaders.ContentType = "text/css";
                    break;

                // script
                case "js":
                    blobHttpHeaders.ContentType = "text/javascript";
                    break;

                // video
                case "mpeg":
                    blobHttpHeaders.ContentType = "audio/mpeg";
                    break;
                case "webm":
                    blobHttpHeaders.ContentType = "video/webm";
                    break;

                // text
                case "txt":
                    blobHttpHeaders.ContentType = "text/plain";
                    break;

                // documents
                case "pdf":
                    blobHttpHeaders.ContentType = "application/pdf";
                    break;
                case "doc":
                case "dot":
                    blobHttpHeaders.ContentType = "application/msword";
                    break;
                case "docx":
                    blobHttpHeaders.ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    break;
                case "dotx":
                    blobHttpHeaders.ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.template";
                    break;
                case "docm":
                    blobHttpHeaders.ContentType = "application/vnd.ms-word.document.macroEnabled.12";
                    break;
                case "dotm":
                    blobHttpHeaders.ContentType = "application/vnd.ms-word.template.macroEnabled.12";
                    break;
                case "xls":
                case "xlt":
                case "xla":
                    blobHttpHeaders.ContentType = "application/vnd.ms-excel";
                    break;
                case "xlsx":
                    blobHttpHeaders.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    break;
                case "xltx":
                    blobHttpHeaders.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.template";
                    break;
                case "xlsm":
                    blobHttpHeaders.ContentType = "application/vnd.ms-excel.sheet.macroEnabled.12";
                    break;
                case "xltm":
                    blobHttpHeaders.ContentType = "application/vnd.ms-excel.template.macroEnabled.12";
                    break;
                case "xlam":
                    blobHttpHeaders.ContentType = "application/vnd.ms-excel.addin.macroEnabled.12";
                    break;
                case "xlsb":
                    blobHttpHeaders.ContentType = "application/vnd.ms-excel.sheet.binary.macroEnabled.12";
                    break;
                case "ppt":
                case "pot":
                case "pps":
                case "ppa":
                    blobHttpHeaders.ContentType = "application/vnd.ms-powerpoint";
                    break;
                case "pptx":
                    blobHttpHeaders.ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                    break;
                case "potx":
                    blobHttpHeaders.ContentType = "application/vnd.openxmlformats-officedocument.presentationml.template";
                    break;
                case "ppsx":
                    blobHttpHeaders.ContentType = "application/vnd.openxmlformats-officedocument.presentationml.slideshow";
                    break;

                // other
                case "ttf":
                    blobHttpHeaders.ContentType = "application/font-sfnt";
                    break;
                case "eot":
                    blobHttpHeaders.ContentType = "application/vnd.ms-fontobject";
                    break;
                case "woff":
                case "woff2":
                    blobHttpHeaders.ContentType = "application/x-font-woff";
                    break;
            }

            blobHttpHeaders.CacheControl = string.Format("public, max-age={0}", IntegerConstants.OneYearInSeconds);

            try
            {
                await blockBlob.SetHttpHeadersAsync(blobHttpHeaders);
            }
            catch (Exception ex)
            {
                throw new Exception("async", ex.InnerException);
            }
        }

        protected async Task SetCorsAsync(BlobServiceClient blobServiceClient)
        {
            // Get the current service properties
            BlobServiceProperties blobServiceProperties = await blobServiceClient.GetPropertiesAsync();

            // Set up a new CORS rule
            BlobCorsRule corsRule = new ()
            {
                AllowedHeaders = "*",
                AllowedMethods = "PUT,GET,HEAD,POST",  // Specify allowed methods as a string
                AllowedOrigins = "*",
                ExposedHeaders ="*",
                MaxAgeInSeconds = IntegerConstants.MaxAgeInSeconds
            };

            blobServiceProperties.Cors = new List<BlobCorsRule> { corsRule };

            // Set the service properties
            await blobServiceClient.SetPropertiesAsync(blobServiceProperties);
        }

        protected async Task SetPublicContainerPermissionsAsync(BlobContainerClient container)
        {
            BlobSignedIdentifier[] signedIdentifiers = Array.Empty<BlobSignedIdentifier>();
            PublicAccessType publicAccessType = PublicAccessType.BlobContainer;

            await container.SetAccessPolicyAsync(publicAccessType, signedIdentifiers);
        }
    }
}
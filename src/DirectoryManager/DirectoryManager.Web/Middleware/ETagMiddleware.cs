using System.Security.Cryptography;

namespace DirectoryManager.Web.Middleware
{
    public class ETagMiddleware
    {
        private readonly RequestDelegate next;

        public ETagMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Buffer the response
            var originalBodyStream = context.Response.Body;
            using (var memoryStream = new MemoryStream())
            {
                context.Response.Body = memoryStream;

                await this.next(context);

                // Only calculate ETag for 200 OK responses
                if (context.Response.StatusCode == StatusCodes.Status200OK)
                {
                    // Compute ETag
                    string eTag = this.GenerateETag(memoryStream);
                    context.Response.Headers["ETag"] = eTag;

                    // Check if ETag matches the If-None-Match header
                    if (context.Request.Headers.TryGetValue("If-None-Match", out var requestETag) && requestETag == eTag)
                    {
                        context.Response.StatusCode = StatusCodes.Status304NotModified;
                        context.Response.Headers["Content-Length"] = "0";
                    }
                    else
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        await memoryStream.CopyToAsync(originalBodyStream);
                    }
                }
                else
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await memoryStream.CopyToAsync(originalBodyStream);
                }
            }
        }

        private string GenerateETag(MemoryStream memoryStream)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(memoryStream.ToArray());
                string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return $"\"{hashString}\""; // ETag should be enclosed in quotes
            }
        }
    }
}
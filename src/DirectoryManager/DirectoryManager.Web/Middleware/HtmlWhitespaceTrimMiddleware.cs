using System.Text;

namespace DirectoryManager.Web.Middleware
{
    /// <summary>
    /// Shrinks rendered HTML by removing the excess whitespace that Razor leaves behind:
    /// whitespace-only (blank) lines, trailing spaces/tabs, and stray carriage returns. It does
    /// NOT touch the meaningful bytes of &lt;pre&gt;/&lt;textarea&gt;/&lt;script&gt;/&lt;style&gt;,
    /// and only ever drops whitespace — so the rendered page is byte-smaller but visually identical.
    /// </summary>
    public sealed class HtmlWhitespaceTrimMiddleware
    {
        private readonly RequestDelegate next;

        public HtmlWhitespaceTrimMiddleware(RequestDelegate next) => this.next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var originalBody = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await this.next(context).ConfigureAwait(false);

                var contentType = context.Response.ContentType ?? string.Empty;
                var isHtml = contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase);

                buffer.Position = 0;

                if (isHtml && !context.Response.HasStarted)
                {
                    using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
                    var html = await reader.ReadToEndAsync().ConfigureAwait(false);
                    var bytes = Encoding.UTF8.GetBytes(HtmlWhitespace.Collapse(html));

                    context.Response.Body = originalBody;
                    context.Response.ContentLength = bytes.Length;
                    await originalBody.WriteAsync(bytes).ConfigureAwait(false);
                }
                else
                {
                    context.Response.Body = originalBody;
                    await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
                }
            }
            finally
            {
                context.Response.Body = originalBody;
            }
        }
    }
}

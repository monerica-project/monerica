using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

public sealed class FileCallbackResult : IActionResult
{
    private readonly string contentType;
    private readonly string fileName;
    private readonly Func<Stream, CancellationToken, Task> writer;

    public FileCallbackResult(string contentType, string fileName, Func<Stream, CancellationToken, Task> writer)
    {
        this.contentType = contentType;
        this.fileName = fileName;
        this.writer = writer;
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var resp = context.HttpContext.Response;
        resp.ContentType = this.contentType;
        resp.Headers[HeaderNames.ContentDisposition] =
            new System.Net.Mime.ContentDisposition { FileName = this.fileName, Inline = false }.ToString();

        try
        {
            await this.writer(resp.Body, context.HttpContext.RequestAborted);
            await resp.Body.FlushAsync(context.HttpContext.RequestAborted);
        }
        catch (OperationCanceledException) { /* client aborted; ignore */ }
        catch (IOException) { /* broken pipe; ignore */ }
    }
}
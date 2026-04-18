using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

[Route("captcha")]
public class CaptchaController : Controller
{
    [HttpGet("image")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Image([FromQuery] string? ctx)
    {
        // Generate and store under a predictable session key
        var code = CaptchaTools.GenerateText(5); // same generator you use elsewhere

        // Normalize the context. If not provided, fall back to a default.
        ctx = string.IsNullOrWhiteSpace(ctx) ? "default" : ctx.Trim();

        // Store BOTH the contexted key AND the legacy key so both consumers work.
        this.HttpContext.Session.SetString($"CaptchaCode:{ctx}", code);
        this.HttpContext.Session.SetString("CaptchaCode", code);

        // Draw PNG (your existing SkiaSharp logic)
        var pngBytes = CaptchaTools.RenderPng(code); // returns byte[]
        return this.File(pngBytes, StringConstants.PngImage);
    }
}

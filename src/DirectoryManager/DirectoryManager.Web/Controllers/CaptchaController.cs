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

        // Store BOTH the contexted key AND the legacy key so both consumers work. The context is
        // normalized (unknown/odd values -> "default") and the number of contexts per session is
        // capped, so this endpoint can't be used to grow a session without limit.
        CaptchaTools.StoreBounded(this.HttpContext.Session, ctx, code);

        // Draw PNG (your existing SkiaSharp logic)
        var pngBytes = CaptchaTools.RenderPng(code); // returns byte[]
        return this.File(pngBytes, StringConstants.PngImage);
    }
}

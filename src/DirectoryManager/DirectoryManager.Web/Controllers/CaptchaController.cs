// Controllers/CaptchaController.cs
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers;

[Route("captcha")]
public class CaptchaController : Controller
{
    // GET /captcha/image?ctx=newsletter  (or ctx=review:123, etc.)
    [HttpGet("image")]
    public IActionResult Image([FromQuery] string ctx, [FromQuery] int w = 120, [FromQuery] int h = 40)
    {
        if (string.IsNullOrWhiteSpace(ctx))
        {
            return this.BadRequest("Missing ctx");
        }

        // create text and store in session for this context
        var text = CaptchaTools.GenerateText(5);
        CaptchaTools.Store(this.HttpContext, ctx, text);

        var png = CaptchaTools.RenderPng(text, w, h);
        return this.File(png, "image/png");
    }
}
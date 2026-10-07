// Helpers/CaptchaTools.cs
using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace DirectoryManager.Web.Helpers;

public static class CaptchaTools
{
    // Session key per-context (lets you run multiple CAPTCHAs at once)
    public static string SessionKey(string ctx) => $"CaptchaCode:{ctx}";

    public static string GenerateText(int length = 5)
    {
        // Ambiguous-looking characters (0/O, 1/I) are excluded. Use a cryptographic RNG
        // rather than System.Random so the challenge isn't predictable/seedable.
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[System.Security.Cryptography.RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(result);
    }

    public static void Store(HttpContext http, string ctx, string captchaText)
    {
        http.Session.SetString(SessionKey(ctx), captchaText);
    }

    public static bool Validate(HttpContext http, string ctx, string userInput, bool consume = true)
    {
        var expected = http.Session.GetString(SessionKey(ctx));
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        var ok = !string.IsNullOrWhiteSpace(userInput) &&
                 string.Equals(userInput.Trim(), expected, StringComparison.OrdinalIgnoreCase);

        if (consume)
        {
            http.Session.Remove(SessionKey(ctx));
        }

        return ok;
    }

    // Render an image exactly like your EmailSubscription controller did
    public static byte[] RenderPng(string text, int width = 120, int height = 40)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        float fontSize = 20;

        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var font = new SKFont { Typeface = typeface, Size = fontSize };

        var glyphs = font.GetGlyphs(text);
        var widths = font.GetGlyphWidths(glyphs);
        float textWidth = widths.Sum();

        var metrics = font.Metrics;
        float textHeight = metrics.Descent - metrics.Ascent;

        float x = (width - textWidth) / 2f;
        float y = ((height + textHeight) / 2f) - metrics.Descent;

        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);

        using var img = SKImage.FromBitmap(bitmap);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
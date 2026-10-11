// Helpers/CaptchaTools.cs
using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace DirectoryManager.Web.Helpers;

public static class CaptchaTools
{
    // Session key per-context (lets you run multiple CAPTCHAs at once)
    public const string LegacySessionKey = "CaptchaCode";

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

            // The image endpoint also stores the code under this legacy key. Clear it too, or
            // the same solved code could be used once more on a form that reads the legacy key.
            http.Session.Remove(LegacySessionKey);
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
        float spacing = 2f;
        float textWidth = widths.Sum() + (spacing * (text.Length - 1));

        var metrics = font.Metrics;
        float textHeight = metrics.Descent - metrics.Ascent;

        float x = (width - textWidth) / 2f;
        float baseY = ((height + textHeight) / 2f) - metrics.Descent;

        // Slight distortion only: enough to trip simple OCR, still easy for a person.
        // A few faint lines behind the text...
        using var linePaint = new SKPaint { Color = new SKColor(150, 150, 150), IsAntialias = true, StrokeWidth = 1f, Style = SKPaintStyle.Stroke };
        for (var i = 0; i < 3; i++)
        {
            canvas.DrawLine(Jitter(0, width / 3), Jitter(0, height), Jitter(2 * width / 3, width), Jitter(0, height), linePaint);
        }

        // ...each character tilted and nudged a little...
        for (var i = 0; i < text.Length; i++)
        {
            float cx = x + (widths[i] / 2f);
            float cy = baseY + Jitter(-3, 3);

            canvas.Save();
            canvas.RotateDegrees(Jitter(-10, 10), cx, cy - (textHeight / 3f));
            canvas.DrawText(text[i].ToString(), x, cy, SKTextAlign.Left, font, paint);
            canvas.Restore();

            x += widths[i] + spacing;
        }

        // ...and a light sprinkle of dots.
        using var dotPaint = new SKPaint { Color = new SKColor(120, 120, 120), IsAntialias = true };
        for (var i = 0; i < 25; i++)
        {
            canvas.DrawCircle(Jitter(0, width), Jitter(0, height), 0.7f, dotPaint);
        }

        using var img = SKImage.FromBitmap(bitmap);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static float Jitter(int minInclusive, int maxInclusive) =>
        minInclusive + (System.Security.Cryptography.RandomNumberGenerator.GetInt32(((maxInclusive - minInclusive) * 10) + 1) / 10f);
}

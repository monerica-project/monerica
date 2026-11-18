// Helpers/CaptchaTools.cs
using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace DirectoryManager.Web.Helpers;

public static class CaptchaTools
{
    // Session key per-context (lets you run multiple CAPTCHAs at once)
    public const string LegacySessionKey = "CaptchaCode";

    // The "ctx" of the captcha image comes from the query string. Every real context is a short
    // lower-case name ("review", "newsletter-unsubscribe", ...), so anything else falls back to
    // "default" instead of becoming a new session key.
    public const int MaxContextLength = 64;

    // A real visitor has at most a few captchas open at once; past this many contexts in one
    // session, the older ones are dropped so a session can't be stuffed with keys.
    public const int MaxContextsPerSession = 8;

    private const string ContextKeyPrefix = "CaptchaCode:";

    public static string SessionKey(string ctx) => $"{ContextKeyPrefix}{ctx}";

    public static string NormalizeContext(string? ctx)
    {
        var c = (ctx ?? string.Empty).Trim();
        if (c.Length == 0 || c.Length > MaxContextLength)
        {
            return "default";
        }

        foreach (var ch in c)
        {
            if (!((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_'))
            {
                return "default";
            }
        }

        return c;
    }

    /// <summary>Stores the expected answer for <paramref name="ctx"/> (and the legacy key) while
    /// keeping the number of captcha contexts in the session bounded.</summary>
    public static void StoreBounded(ISession session, string? ctx, string captchaText)
    {
        var key = SessionKey(NormalizeContext(ctx));

        var others = session.Keys
            .Where(k => k.StartsWith(ContextKeyPrefix, StringComparison.Ordinal) && k != key)
            .ToList();
        if (others.Count >= MaxContextsPerSession)
        {
            // Session keys carry no timestamp, so clear the other contexts; a visitor with that
            // many captchas open just reloads the image on the form they actually submit.
            foreach (var k in others)
            {
                session.Remove(k);
            }
        }

        session.SetString(key, captchaText);
        session.SetString(LegacySessionKey, captchaText);
    }

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

        // Distortion aimed at scripts rather than people (hardened 2026-11-16, after a scanner
        // read the old image with plain OCR): grey lines and dots could be removed by a simple
        // brightness cut, so the noise is now drawn in the SAME black as the letters and ON TOP
        // of them, the letters touch each other, and each one has its own size and tilt.
        using var linePaint = new SKPaint { Color = new SKColor(150, 150, 150), IsAntialias = true, StrokeWidth = 1f, Style = SKPaintStyle.Stroke };
        for (var i = 0; i < 3; i++)
        {
            canvas.DrawLine(Jitter(0, width / 3), Jitter(0, height), Jitter(2 * width / 3, width), Jitter(0, height), linePaint);
        }

        x -= 1f; // letters are drawn slightly closer than their natural spacing
        for (var i = 0; i < text.Length; i++)
        {
            float cx = x + (widths[i] / 2f);
            float cy = baseY + Jitter(-4, 4);

            using var glyphFont = new SKFont { Typeface = typeface, Size = fontSize + Jitter(-2, 3) };
            canvas.Save();
            canvas.RotateDegrees(Jitter(-16, 16), cx, cy - (textHeight / 3f));
            canvas.DrawText(text[i].ToString(), x, cy, SKTextAlign.Left, glyphFont, paint);
            canvas.Restore();

            x += widths[i] + spacing - 1f;
        }

        // Black strokes through the letters (curved, so they can't be found as straight lines).
        using var strikePaint = new SKPaint { Color = SKColors.Black, IsAntialias = true, StrokeWidth = 0.85f, Style = SKPaintStyle.Stroke };
        for (var i = 0; i < 2; i++)
        {
            using var path = new SKPath();
            path.MoveTo(Jitter(0, width / 5), Jitter(height / 4, 3 * height / 4));
            path.CubicTo(
                Jitter(width / 5, 2 * width / 5), Jitter(0, height),
                Jitter(3 * width / 5, 4 * width / 5), Jitter(0, height),
                Jitter(4 * width / 5, width), Jitter(height / 4, 3 * height / 4));
            canvas.DrawPath(path, strikePaint);
        }

        // Black specks, the same color and roughly the same weight as letter strokes.
        using var dotPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        for (var i = 0; i < 30; i++)
        {
            canvas.DrawCircle(Jitter(0, width), Jitter(0, height), Jitter(4, 9) / 10f, dotPaint);
        }

        using var img = SKImage.FromBitmap(bitmap);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static float Jitter(int minInclusive, int maxInclusive) =>
        minInclusive + (System.Security.Cryptography.RandomNumberGenerator.GetInt32(((maxInclusive - minInclusive) * 10) + 1) / 10f);
}

using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Security
{
    public class CaptchaRenderTests
    {
        // Guards the native image library: a package bump that ships without a working
        // Linux native binary would break every captcha-gated form in production.
        [Fact]
        public void RenderPng_ProducesAPngImage()
        {
            var png = CaptchaTools.RenderPng(CaptchaTools.GenerateText());

            Assert.True(png.Length > 200);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
        }
    
        // Writes sample images for a human look when CAPTCHA_SAMPLE_DIR is set; otherwise does nothing.
        [Fact]
        public void RenderPng_WritesSamplesWhenAsked()
        {
            var dir = Environment.GetEnvironmentVariable("CAPTCHA_SAMPLE_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            Directory.CreateDirectory(dir);
            for (var i = 0; i < 8; i++)
            {
                var text = CaptchaTools.GenerateText();
                File.WriteAllBytes(Path.Combine(dir, $"{i}-{text}.png"), CaptchaTools.RenderPng(text));
            }
        }
    }
}

using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Tests.Helpers
{
    public class ListingTextTests
    {
        [Fact]
        public void BrowserLineBreaksCountOnce()
        {
            // 172 letters + 3 line breaks = 175: fits, whether posted with \r\n or \n.
            var text = new string('a', 100) + "\r\n" + new string('b', 40) + "\r\n" + new string('c', 30) + "\r\n" + "dd";
            var entry = new DirectoryEntry { Name = "x", DirectoryEntryKey = "x", Link = "https://x.y", Description = text };
            var sub = new Submission { Description = text };
            Assert.Equal(175, entry.Description!.Length);
            Assert.Equal(entry.Description, sub.Description);
            Assert.DoesNotContain('\r', entry.Description);
        }

        [Fact]
        public void NullStaysNull() => Assert.Null(ListingText.NormalizeLineBreaks(null));
    }
}

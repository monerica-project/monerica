using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Review;
using DirectoryManager.SubmissionReviewer.Text;
using Xunit;

namespace DirectoryManager.SubmissionReviewer.Tests
{
    public class PgpKeyScanTests
    {
        // A real public key (LNVPS.net, published at https://lnvps.net/lnvps.asc).
        internal const string Key =
            "-----BEGIN PGP PUBLIC KEY BLOCK-----\n" +
            "\n" +
            "mDMEZzz3SxYJKwYBBAHaRw8BAQdA+aAQWjWUIzS7gD2Cq83yQK+CJtWEuqv1cAFU\n" +
            "jwvLpRi0G0xOVlBTLm5ldCA8c2FsZXNAbG52cHMubmV0PoiZBBMWCgBBFiEEnQ44\n" +
            "TyK1cOyGQUo0BUVCibEtEd4FAmc890sCGwMFCQWjmoAFCwkIBwICIgIGFQoJCAsC\n" +
            "BBYCAwECHgcCF4AACgkQBUVCibEtEd45mAD9E0Iybxqt7Cb+nyC3Nu7QeL1zBy4G\n" +
            "iCJgB+RcLZDRQCEBANDIzM90nFN9ojmsfte8zH+v71tEcC8IokIL0Ehyn4gIuDgE\n" +
            "Zzz3SxIKKwYBBAGXVQEFAQEHQPGJhUgGvYn82mlGTfo080tHD9j5EyijsMpFImT4\n" +
            "argrAwEIB4h+BBgWCgAmFiEEnQ44TyK1cOyGQUo0BUVCibEtEd4FAmc890sCGwwF\n" +
            "CQWjmoAACgkQBUVCibEtEd7UkQD/cazecmF3JHQ+Vk8/3zozRNMNRO78gMKS+Ve8\n" +
            "+0Q/kpEBANEW3Z0501WNrGJf/jnlBf4VUH+XPA1i4nq9U4DLjD0G\n" +
            "=71iA\n" +
            "-----END PGP PUBLIC KEY BLOCK-----\n";

        private static readonly Uri Home = new ("https://lnvps.net/");

        [Fact] // HAPPY: a plain .asc file comes back unchanged
        public void Extract_PlainKeyFile()
        {
            Assert.Equal(Key, PgpKeyScan.ExtractArmoredKey(Key));
        }

        [Fact] // HAPPY: a key shown in a page (<pre> with <br> and entities, CRLF, Comment header) is cleaned to plain armor
        public void Extract_KeyInsideHtml()
        {
            var html = "<html><body><h2>Our PGP key</h2><pre>" +
                Key.Replace("\n", "<br>\r\n").Replace("BLOCK-----<br>\r\n<br>", "BLOCK-----<br>\r\nComment: sales&#64;lnvps.net<br>\r\n<br>") +
                "</pre></body></html>";

            var key = PgpKeyScan.ExtractArmoredKey(html);

            Assert.NotNull(key);
            Assert.StartsWith(PgpKeyScan.BeginMarker + "\nComment: sales@lnvps.net\n\nmDMEZzz3", key, StringComparison.Ordinal);
            Assert.EndsWith("=71iA\n" + PgpKeyScan.EndMarker + "\n", key, StringComparison.Ordinal);
            Assert.DoesNotContain("<", key, StringComparison.Ordinal);
        }

        [Theory] // SAD: no key, a cut-off key, junk inside the armor, a private key, a tiny fake block
        [InlineData("<p>Contact us by email.</p>")]
        [InlineData("-----BEGIN PGP PUBLIC KEY BLOCK-----\n\nmDMEZzz3SxYJKwYBBAHaRw8BAQdA")]
        [InlineData("-----BEGIN PGP PUBLIC KEY BLOCK-----\n\nthis is not base64 at all!!\n-----END PGP PUBLIC KEY BLOCK-----")]
        [InlineData("-----BEGIN PGP PRIVATE KEY BLOCK-----\n\nlQOYBF\n-----END PGP PRIVATE KEY BLOCK-----")]
        [InlineData("-----BEGIN PGP PUBLIC KEY BLOCK-----\n\nabc\n-----END PGP PUBLIC KEY BLOCK-----")]
        public void Extract_RejectsNonKeys(string content)
        {
            Assert.Null(PgpKeyScan.ExtractArmoredKey(content));
        }

        [Fact] // HAPPY: .asc links rank first, then "PGP" pages; other sites' key files are ignored, keys.openpgp.org is allowed
        public void KeyLinks_FindsOwnKeyFilesAndPgpPages()
        {
            var html = "<a href=\"/contact\">PGP key</a>" +
                "<a href=\"/lnvps.asc\">PGP</a>" +
                "<a href=\"https://evil.example/key.asc\">key</a>" +
                "<a href=\"https://keys.openpgp.org/vks/v1/by-fingerprint/9D0E384F22B570EC86414A3405454289B12D11DE\">keyserver</a>" +
                "<a href=\"/pricing\">Pricing</a>";

            var links = PgpKeyScan.KeyLinks(html, Home);

            Assert.Equal(
                new[]
                {
                    "https://lnvps.net/lnvps.asc",
                    "https://keys.openpgp.org/vks/v1/by-fingerprint/9D0E384F22B570EC86414A3405454289B12D11DE",
                    "https://lnvps.net/contact",
                },
                links);
        }

        [Fact] // HAPPY: the dossier builder follows the linked .asc file and returns the key with its source
        public async Task FindPgpKey_FollowsLinkedKeyFile()
        {
            var fetcher = new KeyFetcher(new Dictionary<string, string> { ["https://lnvps.net/lnvps.asc"] = Key });
            var builder = new SubmissionDossierBuilder(fetcher);
            var main = new FetchResult(Home.ToString(), true, 200, "<footer><a href=\"/lnvps.asc\">PGP</a></footer>", null, Home);

            var found = await builder.FindPgpKeyAsync(main, null, CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(Key, found!.Value.Key);
            Assert.Equal("https://lnvps.net/lnvps.asc", found.Value.Source);
        }

        [Fact] // SAD: no key anywhere → null, and nothing beyond the linked candidates is fetched
        public async Task FindPgpKey_NoneFound()
        {
            var fetcher = new KeyFetcher(new Dictionary<string, string>());
            var builder = new SubmissionDossierBuilder(fetcher);
            var main = new FetchResult(Home.ToString(), true, 200, "<a href=\"/about\">About</a>", null, Home);

            Assert.Null(await builder.FindPgpKeyAsync(main, null, CancellationToken.None));
            Assert.Empty(fetcher.Requested);
        }

        private sealed class KeyFetcher : IPageFetcher
        {
            private readonly Dictionary<string, string> bodies;

            public KeyFetcher(Dictionary<string, string> bodies) => this.bodies = bodies;

            public List<string> Requested { get; } = new ();

            public Task<FetchResult> FetchAsync(string url, CancellationToken ct)
            {
                this.Requested.Add(url);
                return Task.FromResult(this.bodies.TryGetValue(url, out var body)
                    ? new FetchResult(url, true, 200, body, null, new Uri(url))
                    : new FetchResult(url, true, 404, null, "HTTP 404", new Uri(url)));
            }
        }
    }
}

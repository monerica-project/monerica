using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Web.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Which member accounts currently have a proven PGP key linked (account id → fingerprint),
    /// for bylines and admin pages. Display follows the account's CURRENT state: a post shows the
    /// key only while the account still holds it, so after "Remove key" or "Replace key" no page
    /// keeps claiming the old key. One small query (only accounts with a key), cached in memory
    /// and dropped on every link / unlink / account deletion.
    /// </summary>
    public sealed class MemberKeyDirectory
    {
        private const string CacheKey = "member-linked-pgp-keys";
        private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

        private readonly ApplicationDbContext db;
        private readonly IMemoryCache cache;

        public MemberKeyDirectory(ApplicationDbContext db, IMemoryCache cache)
        {
            this.db = db;
            this.cache = cache;
        }

        public static void Invalidate(IMemoryCache cache) => cache.Remove(CacheKey);

        /// <summary>The linked key's fingerprint for a member post's author, or null (none / not a member post).</summary>
        public async Task<string?> FingerprintForAsync(string? authorUserId)
        {
            if (!MemberAuthor.IsMember(authorUserId))
            {
                return null;
            }

            var map = await this.GetAllAsync();
            return map.TryGetValue(authorUserId!, out var fp) ? fp : null;
        }

        public async Task<IReadOnlyDictionary<string, string>> GetAllAsync()
        {
            if (this.cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, string>? cached) && cached != null)
            {
                return cached;
            }

            var rows = await this.db.Users.AsNoTracking()
                .Where(u => u.PgpFingerprint != null && u.DeletedUtc == null)
                .Select(u => new { u.Id, u.PgpFingerprint })
                .ToListAsync();

            IReadOnlyDictionary<string, string> map = rows.ToDictionary(r => r.Id, r => r.PgpFingerprint!, StringComparer.Ordinal);
            this.cache.Set(CacheKey, map, CacheFor);
            return map;
        }
    }
}

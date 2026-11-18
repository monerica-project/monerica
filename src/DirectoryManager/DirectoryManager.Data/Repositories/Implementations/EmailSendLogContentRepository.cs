using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class EmailSendLogContentRepository : IEmailSendLogContentRepository
    {
        private readonly ApplicationDbContext db;

        public EmailSendLogContentRepository(ApplicationDbContext db)
        {
            this.db = db;
        }

        public int GetOrCreate(EmailSendLogContent candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            if (string.IsNullOrWhiteSpace(candidate.ContentHash))
            {
                throw new ArgumentException("ContentHash is required.", nameof(candidate));
            }

            var now = DateTime.UtcNow;
            var existingId = this.TouchExisting(candidate.ContentHash, now);
            if (existingId.HasValue)
            {
                return existingId.Value;
            }

            candidate.LastUsedUtc = now;
            this.db.EmailSendLogContents.Add(candidate);
            try
            {
                this.db.SaveChanges();
                return candidate.EmailSendLogContentId;
            }
            catch (DbUpdateException)
            {
                // Another sender inserted the same content between our lookup and insert
                // (unique ContentHash): use that row.
                this.db.Entry(candidate).State = EntityState.Detached;
                existingId = this.TouchExisting(candidate.ContentHash, now);
                if (existingId.HasValue)
                {
                    return existingId.Value;
                }

                throw;
            }
        }

        public EmailSendLogContent? Get(int emailSendLogContentId) =>
            this.db.EmailSendLogContents.AsNoTracking()
                .FirstOrDefault(c => c.EmailSendLogContentId == emailSendLogContentId);

        public async Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken ct = default)
        {
            if (this.db.Database.IsRelational())
            {
                // The FK is ON DELETE SET NULL; clearing first keeps it explicit and provider-independent.
                var oldIds = this.db.EmailSendLogContents
                    .Where(c => c.LastUsedUtc < olderThanUtc)
                    .Select(c => (int?)c.EmailSendLogContentId);
                await this.db.EmailSendLogs
                    .Where(l => oldIds.Contains(l.EmailSendLogContentId))
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.EmailSendLogContentId, (int?)null), ct);
                return await this.db.EmailSendLogContents
                    .Where(c => c.LastUsedUtc < olderThanUtc)
                    .ExecuteDeleteAsync(ct);
            }

            var old = await this.db.EmailSendLogContents.Where(c => c.LastUsedUtc < olderThanUtc).ToListAsync(ct);
            var ids = old.Select(c => c.EmailSendLogContentId).ToList();
            var logs = await this.db.EmailSendLogs.Where(l => l.EmailSendLogContentId != null && ids.Contains(l.EmailSendLogContentId.Value)).ToListAsync(ct);
            foreach (var l in logs)
            {
                l.EmailSendLogContentId = null;
            }

            this.db.EmailSendLogContents.RemoveRange(old);
            await this.db.SaveChangesAsync(ct);
            return old.Count;
        }

        private int? TouchExisting(string hash, DateTime now)
        {
            var existing = this.db.EmailSendLogContents.FirstOrDefault(c => c.ContentHash == hash);
            if (existing == null)
            {
                return null;
            }

            if (existing.LastUsedUtc < now)
            {
                existing.LastUsedUtc = now;
                this.db.SaveChanges();
            }

            return existing.EmailSendLogContentId;
        }
    }
}

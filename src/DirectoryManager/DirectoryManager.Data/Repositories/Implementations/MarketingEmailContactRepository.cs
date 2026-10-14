using System.Security.Cryptography;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class MarketingEmailContactRepository : IMarketingEmailContactRepository
    {
        private readonly IApplicationDbContext context;

        public MarketingEmailContactRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public static string NormalizeEmail(string? email) =>
            (email ?? string.Empty).Trim().ToLowerInvariant();

        public static string NewToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

        public async Task<MarketingEmailContact?> GetByTokenAsync(string token)
        {
            token = (token ?? string.Empty).Trim().ToLowerInvariant();
            if (token.Length == 0)
            {
                return null;
            }

            return await this.context.MarketingEmailContacts.FirstOrDefaultAsync(x => x.Token == token);
        }

        public async Task<MarketingEmailContact?> GetByEmailAsync(string email)
        {
            email = NormalizeEmail(email);
            if (email.Length == 0)
            {
                return null;
            }

            return await this.context.MarketingEmailContacts.FirstOrDefaultAsync(x => x.Email == email);
        }

        public async Task<MarketingEmailContact> GetOrCreateAsync(string email, string? note = null)
        {
            email = NormalizeEmail(email);
            if (email.Length == 0)
            {
                throw new ArgumentException("email is required", nameof(email));
            }

            var existing = await this.context.MarketingEmailContacts.FirstOrDefaultAsync(x => x.Email == email);
            if (existing != null)
            {
                return existing;
            }

            var contact = new MarketingEmailContact
            {
                Email = email,
                Token = NewToken(),
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CreateDate = DateTime.UtcNow
            };

            await this.context.MarketingEmailContacts.AddAsync(contact);
            await this.context.SaveChangesAsync();
            return contact;
        }

        public async Task<bool> SetOptedOutAsync(int marketingEmailContactId, bool optedOut, string source)
        {
            var contact = await this.context.MarketingEmailContacts
                .FirstOrDefaultAsync(x => x.MarketingEmailContactId == marketingEmailContactId);
            if (contact == null)
            {
                return false;
            }

            if (optedOut)
            {
                // Keep the original opt-out date if they were already opted out.
                contact.OptedOutUtc ??= DateTime.UtcNow;
                contact.OptOutSource ??= source;
            }
            else
            {
                contact.OptedOutUtc = null;
                contact.OptOutSource = null;
            }

            await this.context.SaveChangesAsync();
            return true;
        }

        public async Task<int> CountAsync(bool optedOutOnly)
        {
            return await this.Filtered(optedOutOnly).CountAsync();
        }

        public async Task<List<MarketingEmailContact>> ListPageAsync(int page, int pageSize, bool optedOutOnly)
        {
            page = Math.Max(1, page);
            return await this.Filtered(optedOutOnly)
                .OrderBy(x => x.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        private IQueryable<MarketingEmailContact> Filtered(bool optedOutOnly)
        {
            IQueryable<MarketingEmailContact> q = this.context.MarketingEmailContacts.AsNoTracking();
            return optedOutOnly ? q.Where(x => x.OptedOutUtc != null) : q;
        }
    }
}

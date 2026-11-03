using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// "Could this waitlisted listing buy that spot right now?" for waitlist rows. One place, used by
    /// the admin notification list and the public waitlist, with the same rules as checkout
    /// (<see cref="SponsorshipEligibilityHelper.ForPlacement"/>).
    /// </summary>
    public class WaitlistEligibilityService
    {
        private readonly IDirectoryEntryRepository entryRepo;
        private readonly ISubcategoryRepository subcategoryRepo;
        private readonly IDirectoryEntryAdditionalSubcategoryRepository additionalSubcategoryRepo;
        private readonly ISponsoredListingRepository sponsoredListingRepo;

        public WaitlistEligibilityService(
            IDirectoryEntryRepository entryRepo,
            ISubcategoryRepository subcategoryRepo,
            IDirectoryEntryAdditionalSubcategoryRepository additionalSubcategoryRepo,
            ISponsoredListingRepository sponsoredListingRepo)
        {
            this.entryRepo = entryRepo;
            this.subcategoryRepo = subcategoryRepo;
            this.additionalSubcategoryRepo = additionalSubcategoryRepo;
            this.sponsoredListingRepo = sponsoredListingRepo;
        }

        /// <returns>Waitlist row id → (eligible now, short reason when not).</returns>
        public async Task<Dictionary<int, (bool Eligible, string? Reason)>> ForRowsAsync(
            IEnumerable<(int Id, SponsorshipType Type, int? TypeId, int? DirectoryEntryId)> rows)
        {
            var list = rows.ToList();
            var result = new Dictionary<int, (bool Eligible, string? Reason)>();
            if (list.Count == 0)
            {
                return result;
            }

            var entries = new Dictionary<int, DirectoryManager.Data.Models.DirectoryEntry>();
            foreach (var entryId in list.Where(x => x.DirectoryEntryId is > 0).Select(x => x.DirectoryEntryId!.Value).Distinct())
            {
                var entry = await this.entryRepo.GetByIdAsync(entryId);
                if (entry != null)
                {
                    entries[entryId] = entry;
                }
            }

            var subcategories = (await this.subcategoryRepo.GetAllAsync()).ToDictionary(s => s.SubCategoryId);
            var activeSponsors = await this.sponsoredListingRepo.GetActivelySponsoredDirectoryEntryIdsAsync();
            var alternatives = new Dictionary<int, List<(int SubCategoryId, bool SponsorshipDisabled)>>();
            var now = DateTime.UtcNow;

            foreach (var n in list)
            {
                if (n.DirectoryEntryId is not int eid || !entries.TryGetValue(eid, out var e))
                {
                    result[n.Id] = (false, "No listing linked");
                    continue;
                }

                subcategories.TryGetValue(e.SubCategoryId, out var mainSub);
                if (!alternatives.TryGetValue(eid, out var alts))
                {
                    alts = (await this.additionalSubcategoryRepo.GetByDirectoryEntryIdAsync(eid))
                        .Select(a => (a.SubCategoryId, subcategories.TryGetValue(a.SubCategoryId, out var s) && s.SponsorshipDisabled))
                        .ToList();
                    alternatives[eid] = alts;
                }

                result[n.Id] = SponsorshipEligibilityHelper.ForPlacement(
                    n.Type,
                    n.TypeId,
                    e.DirectoryStatus,
                    e.CreateDate,
                    e.SubCategoryId,
                    mainSub?.CategoryId,
                    mainSub?.SponsorshipDisabled == true,
                    alts,
                    activeSponsors.Contains(eid),
                    DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring,
                    now);
            }

            return result;
        }
    }
}

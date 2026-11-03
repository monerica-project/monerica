using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class WaitlistPublicItemVm
    {
        public string ListingName { get; set; } = "";
        public string ListingUrl { get; set; } = "";
        public DateTime JoinedUtc { get; set; }

        /// <summary>True when this listing could buy a sponsorship today.</summary>
        public bool IsEligible { get; set; }

        /// <summary>"Yes", "Needs verification", or when it will be (time listed).</summary>
        public string Eligibility { get; set; } = string.Empty;
    }
}

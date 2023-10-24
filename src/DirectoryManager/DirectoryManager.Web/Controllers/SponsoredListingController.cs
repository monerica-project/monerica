using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;

namespace DirectoryManager.Web.Controllers
{
    [Route("sponsoredlisting")]
    public class SponsoredListingController : BaseController
    {
        private readonly ISubCategoryRepository subCategoryRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository;
        private readonly INowPaymentsService paymentService;
        private readonly IMemoryCache cache;
        private readonly SponsoredListingOffersContainer sponsoredListings;
        private readonly ILogger<SponsoredListingController> logger;

        public SponsoredListingController(
            ISubCategoryRepository subCategoryRepository,
            IDirectoryEntryRepository directoryEntryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            ITrafficLogRepository trafficLogRepository,
            INowPaymentsService paymentService,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache,
            SponsoredListingOffersContainer sponsoredListings,
            ILogger<SponsoredListingController> logger)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.subCategoryRepository = subCategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.sponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
            this.paymentService = paymentService;
            this.cache = cache;
            this.sponsoredListings = sponsoredListings;
            this.logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> IndexAsync()
        {
            var currentListings = await this.sponsoredListingRepository.GetAllActiveListingsAsync();
            var model = new SponsoredListingHomeModel();

            if (currentListings != null && currentListings.Any())
            {
                var count = currentListings.Count();

                model.CurrentListingCount = count;

                if (count == IntegerConstants.MaxSponsoredListings)
                {
                    // max listings reached
                    model.CanCreateSponsoredListing = false;
                }
                else
                {
                    model.CanCreateSponsoredListing = true;
                }

                // Get the next listing expiration date (i.e., the soonest CampaignEndDate)
                model.NextListingExpiration = currentListings.Min(x => x.CampaignEndDate);
            }
            else
            {
                model.CanCreateSponsoredListing = true;
            }

            return this.View(model);
        }

        [AllowAnonymous]
        [HttpGet("current")]
        public IActionResult Current()
        {
            return this.View();
        }

        [AllowAnonymous]
        [HttpGet("selectduration")]
        public async Task<IActionResult> SelectDurationAsync(int id)
        {
            // todo: don't allow a second listing if there is one already active
            var currentListings = await this.sponsoredListingRepository.GetAllActiveListingsAsync();

            if (currentListings != null)
            {
                if (currentListings.Count() == IntegerConstants.MaxSponsoredListings)
                {
                    // max listings
                    return this.BadRequest(new { Error = "Maximum number of sponsored listings reached." });
                }

                if (currentListings.FirstOrDefault(x => x.DirectoryEntryId == id) != null)
                {
                    // this listing is already active
                    return this.BadRequest(new { Error = "This listing is already active." });
                }
            }

            var model = this.sponsoredListings
                            .SponsoredListingOffers
                            .OrderBy(x => x.Days);

            return this.View(model);
        }

        [AllowAnonymous]
        [HttpPost("selectduration")]
        public async Task<IActionResult> SelectDurationAsync(int id, int selectedOfferId)
        {
            var selectedOffer = this.sponsoredListings
                                .SponsoredListingOffers
                                .Where(x => x.Id == selectedOfferId);

            if (selectedOffer == null)
            {
                return this.BadRequest(new { Error = "Invalid offer selection." });
            }

            var selectedDiretoryEntry = await this.directoryEntryRepository.GetByIdAsync(id);

            if (selectedDiretoryEntry == null)
            {
                return this.BadRequest(new { Error = "Directory entry not found." });
            }

            return this.RedirectToAction(
                        "ConfirmNowPayments",
                        new { directoryEntryId = id, selectedOfferId = selectedOfferId });
        }

        [AllowAnonymous]
        [HttpGet("selectlisting")]
        public async Task<IActionResult> SelectListing(int? subCategoryId = null)
        {
            var entries = await this.directoryEntryRepository.GetAllowableEntries();

            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategoryId == subCategoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name)
                             .ToList();

            this.ViewBag.SubCategories = (await this.subCategoryRepository
                                                    .GetAllActiveSubCategoriesAsync())
                                                    .OrderBy(sc => sc.Category.Name)
                                                    .ThenBy(sc => sc.Name)
                                                    .ToList();

            return this.View("SelectListing", entries);
        }

        [AllowAnonymous]
        [HttpGet("nowpaymentssuccess")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter",  Justification = "This is the param from them")]
        public async Task<IActionResult> NowPaymentsSuccess([FromQuery] string NP_id)
        {
            var processorInvoice = await this.paymentService.GetPaymentStatusAsync(NP_id);

            if (processorInvoice == null)
            {
                return this.BadRequest(new { Error = "Invoice not found." });
            }

            if (processorInvoice.OrderId == null)
            {
                return this.BadRequest(new { Error = "Order ID not found." });
            }

            var existingInvoice = await this.sponsoredListingInvoiceRepository
                                            .GetByInvoiceIdAsync(Guid.Parse(processorInvoice.OrderId));

            if (existingInvoice == null)
            {
                return this.BadRequest(new { Error = "Invoice not found." });
            }

            existingInvoice.PaymentStatus = PaymentStatus.Paid;
            existingInvoice.PaymentResponse = NP_id;

            await this.CreateNewSponsoredListing(existingInvoice);

            var viewModel = new SuccessViewModel
            {
                OrderId = existingInvoice.InvoiceId
            };

            return this.View("NowPaymentsSuccess", viewModel);
        }

        [AllowAnonymous]
        [HttpGet("confirmnowpayments")]
        public async Task<IActionResult> ConfirmNowPaymentsAsync(
            int directoryEntryId,
            int selectedOfferId)
        {
            var offer = this.sponsoredListings.SponsoredListingOffers.FirstOrDefault(x => x.Id == selectedOfferId);
            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);

            if (offer == null || directoryEntry == null)
            {
                return this.BadRequest(new { Error = "Invalid selection." });
            }

            var viewModel = new ConfirmSelectionViewModel
            {
                SelectedDirectoryEntry = new DirectoryEntryViewModel()
                {
                    DirectoryEntry = directoryEntry,
                },
                Offer = offer
            };

            var currentListings = await this.sponsoredListingRepository.GetAllActiveListingsAsync();
            if (currentListings != null && currentListings.Any())
            {
                if (currentListings.Count() == IntegerConstants.MaxSponsoredListings)
                {
                    // max listings reached
                    viewModel.CanCreateSponsoredListing = false;
                }
                else
                {
                    viewModel.CanCreateSponsoredListing = true;
                }

                // Get the next listing expiration date (i.e., the soonest CampaignEndDate)
                viewModel.NextListingExpiration = currentListings.Min(x => x.CampaignEndDate);
            }
            else
            {
                viewModel.CanCreateSponsoredListing = true;
            }

            return this.View(viewModel);
        }

        [AllowAnonymous]
        [HttpPost("confirmnowpayments")]
        public async Task<IActionResult> ConfirmedNowPaymentsAsync(
            int directoryEntryId,
            int selectedOfferId)
        {
            var sponsoredListingOffer = this.sponsoredListings
                                            .SponsoredListingOffers
                                            .FirstOrDefault(x => x.Id == selectedOfferId);

            if (sponsoredListingOffer == null)
            {
                return this.BadRequest(new { Error = "Invalid offer selection." });
            }

            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(directoryEntryId);

            if (directoryEntry == null)
            {
                return this.BadRequest(new { Error = "Directory entry not found." });
            }

            var now = DateTime.UtcNow;

            var invoice = await this.sponsoredListingInvoiceRepository.CreateAsync(
                new SponsoredListingInvoice
                {
                    DirectoryEntryId = directoryEntryId,
                    Currency = Currency.USD,
                    InvoiceId = Guid.NewGuid(),
                    PaymentStatus = PaymentStatus.InvoiceCreated,
                    CampaignStartDate = now,
                    CampaignEndDate = now.AddDays(sponsoredListingOffer.Days),
                    Amount = sponsoredListingOffer.USDPrice,
                    InvoiceDescription = sponsoredListingOffer.Description
                });

            var invoiceRequest = new PaymentRequest
            {
                IsFeePaidByUser = true,
                PriceAmount = sponsoredListingOffer.USDPrice,
                PriceCurrency = this.paymentService.PriceCurrency,
                PayCurrency = this.paymentService.PayCurrency,
                OrderId = invoice.InvoiceId.ToString(),
                OrderDescription = sponsoredListingOffer.Description
            };

            this.paymentService.SetDefaultUrls(invoiceRequest);

            var invoiceFromProcessor = await this.paymentService.CreateInvoice(invoiceRequest);

            if (invoiceFromProcessor == null)
            {
                return this.BadRequest(new { Error = "Failed to create invoice." });
            }

            if (invoiceFromProcessor.Id == null)
            {
                return this.BadRequest(new { Error = "Failed to create invoice ID." });
            }

            invoice.ProcessorInvoiceId = invoiceFromProcessor.Id;
            invoice.PaymentProcessor = PaymentProcessor.NOWPayments;
            invoice.InvoiceRequest = JsonConvert.SerializeObject(invoiceRequest);
            invoice.InvoiceResponse = JsonConvert.SerializeObject(invoiceFromProcessor);

            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);

            if (string.IsNullOrWhiteSpace(invoiceFromProcessor?.InvoiceUrl))
            {
                return this.BadRequest(new { Error = "Failed to get invoice URL." });
            }

            return this.Redirect(invoiceFromProcessor.InvoiceUrl);
        }

        [AllowAnonymous]
        [HttpPost("nowpaymentscallback")]
        public async Task<IActionResult> NowPaymentsCallBackAsync()
        {
            using var reader = new StreamReader(this.Request.Body, Encoding.UTF8);
            var callbackPayload = await reader.ReadToEndAsync();

            this.logger.LogError(callbackPayload);

            IpnPaymentMessage? ipnMessage = null;
            try
            {
                ipnMessage = JsonConvert.DeserializeObject<IpnPaymentMessage>(callbackPayload);
            }
            catch (JsonException)
            {
                return this.BadRequest(new { Error = "Failed to parse the request body." });
            }

            if (ipnMessage == null)
            {
                return this.BadRequest(new { Error = "Deserialized object is null." });
            }

            var nowPaymentsSig = this.Request
                                     .Headers[NowPayments.API.Constants.StringConstants.HeaderNameAuthCallBack]
                                     .FirstOrDefault() ?? string.Empty;

            bool isValidRequest = this.paymentService.IsIpnRequestValid(
                callbackPayload,
                nowPaymentsSig,
                out string errorMsg);

            if (!isValidRequest)
            {
                return this.BadRequest(new { Error = errorMsg });
            }

            if (ipnMessage == null)
            {
                return this.BadRequest(new { Error = "Deserialized object is null." });
            }

            if (ipnMessage.OrderId == null)
            {
                return this.BadRequest(new { Error = "Order ID is null." });
            }

            var invoice = await this.sponsoredListingInvoiceRepository
                                    .GetByInvoiceIdAsync(Guid.Parse(ipnMessage.OrderId));

            if (invoice == null)
            {
                return this.BadRequest(new { Error = "Invoice not found." });
            }

            invoice.PaymentResponse = JsonConvert.SerializeObject(ipnMessage);
            invoice.PaidAmount = ipnMessage.PayAmount;

            if (ipnMessage == null)
            {
                return this.BadRequest(new { Error = "Deserialized object is null." });
            }

            if (ipnMessage.PaymentStatus == null)
            {
                return this.BadRequest(new { Error = "Payment status is null." });
            }

            var processorPaymentStatus = EnumHelper.ParseStringToEnum<NowPayments.API.Enums.PaymentStatus>(ipnMessage.PaymentStatus);
            var translatedValue = ConvertToInternalStatus(processorPaymentStatus);
            invoice.PaymentStatus = translatedValue;

            if (ipnMessage.PayCurrency == null)
            {
                return this.BadRequest(new { Error = "Pay currency is null." });
            }

            var processorCurrency = EnumHelper.ParseStringToEnum<Currency>(ipnMessage.PayCurrency);
            invoice.PaidInCurrency = processorCurrency;

            await this.CreateNewSponsoredListing(invoice);

            return this.Ok();
        }

        [HttpGet("edit/{id}")]
        [Authorize]
        public async Task<IActionResult> EditAsync(int id)
        {
            var listing = await this.sponsoredListingRepository.GetByIdAsync(id);
            if (listing == null)
            {
                return this.NotFound();
            }

            var model = new EditListingViewModel
            {
                Id = listing.SponsoredListingId,
                CampaignStartDate = listing.CampaignStartDate,
                CampaignEndDate = listing.CampaignEndDate
            };

            return this.View(model);
        }

        [HttpPost("edit/{id}")]
        [Authorize]
        public async Task<IActionResult> EditAsync(int id, EditListingViewModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var listing = await this.sponsoredListingRepository.GetByIdAsync(id);
            if (listing == null)
            {
                return this.NotFound();
            }

            listing.CampaignStartDate = model.CampaignStartDate;
            listing.CampaignEndDate = model.CampaignEndDate;

            await this.sponsoredListingRepository.UpdateAsync(listing);

            this.cache.Remove(StringConstants.EntriesCacheKey);
            this.cache.Remove(StringConstants.SponsoredListingsCacheKey);

            return this.RedirectToAction("List");
        }

        [HttpGet("list/{page?}")]
        [Authorize]
        public async Task<IActionResult> List(int page = 1)
        {
            int pageSize = 10;
            var totalListings = await this.sponsoredListingRepository.GetTotalCountAsync();
            var listings = await this.sponsoredListingRepository.GetPaginatedListingsAsync(page, pageSize);

            var model = new PaginatedListingsViewModel
            {
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalListings / (double)pageSize),
                Listings = listings.Select(l => new ListingViewModel
                {
                    Id = l.SponsoredListingId,
                    DirectoryEntryName = l.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                    StartDate = l.CampaignStartDate,
                    EndDate = l.CampaignEndDate
                }).ToList()
            };

            return this.View(model);
        }

        private static DirectoryManager.Data.Enums.PaymentStatus ConvertToInternalStatus(
            NowPayments.API.Enums.PaymentStatus externalStatus)
        {
            switch (externalStatus)
            {
                case NowPayments.API.Enums.PaymentStatus.Unknown:
                    return DirectoryManager.Data.Enums.PaymentStatus.Unknown;

                case NowPayments.API.Enums.PaymentStatus.Waiting:
                    return DirectoryManager.Data.Enums.PaymentStatus.InvoiceCreated;

                case NowPayments.API.Enums.PaymentStatus.Sending:
                case NowPayments.API.Enums.PaymentStatus.Confirming:
                case NowPayments.API.Enums.PaymentStatus.Confirmed:
                    return DirectoryManager.Data.Enums.PaymentStatus.Pending;

                case NowPayments.API.Enums.PaymentStatus.Finished:
                    return DirectoryManager.Data.Enums.PaymentStatus.Paid;

                case NowPayments.API.Enums.PaymentStatus.PartiallyPaid:
                    return DirectoryManager.Data.Enums.PaymentStatus.UnderPayment;

                case NowPayments.API.Enums.PaymentStatus.Failed:
                case NowPayments.API.Enums.PaymentStatus.Refunded:
                    return DirectoryManager.Data.Enums.PaymentStatus.Failed;

                case NowPayments.API.Enums.PaymentStatus.Expired:
                    return DirectoryManager.Data.Enums.PaymentStatus.Expired;

                default:
                    throw new ArgumentOutOfRangeException(nameof(externalStatus), externalStatus, null);
            }
        }

        private async Task CreateNewSponsoredListing(SponsoredListingInvoice invoice)
        {
            if (await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice))
            {
                var existingSponsoredListing = await this.sponsoredListingRepository
                                                         .GetByInvoiceIdAsync(invoice.SponsoredListingInvoiceId);

                if (existingSponsoredListing == null && invoice.PaymentStatus == PaymentStatus.Paid)
                {
                    await this.sponsoredListingRepository.CreateAsync(
                        new SponsoredListing()
                        {
                            DirectoryEntryId = invoice.DirectoryEntryId,
                            CampaignStartDate = invoice.CampaignStartDate,
                            CampaignEndDate = invoice.CampaignEndDate,
                            SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId,
                        });

                    this.ClearCachedItems();
                }
            }
        }
    }
}
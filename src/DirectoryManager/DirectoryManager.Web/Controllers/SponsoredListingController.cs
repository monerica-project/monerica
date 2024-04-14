using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
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
        private readonly ISponsoredListingOfferRepository sponsoredListingOfferRepository;
        private readonly ISponsoredListingReservationRepository sponsoredListingReservationRepository;
        private readonly ICacheService cacheService;
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
            ISponsoredListingOfferRepository sponsoredListings,
            ISponsoredListingReservationRepository sponsoredListingReservationRepository,
            ICacheService cacheService,
            ILogger<SponsoredListingController> logger)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.subCategoryRepository = subCategoryRepository;
            this.directoryEntryRepository = directoryEntryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.sponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
            this.paymentService = paymentService;
            this.cache = cache;
            this.sponsoredListingOfferRepository = sponsoredListings;
            this.sponsoredListingReservationRepository = sponsoredListingReservationRepository;
            this.cacheService = cacheService;
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

                if (count >= IntegerConstants.MaxSponsoredListings)
                {
                    // max listings reached
                    model.CanCreateSponsoredListing = false;
                }
                else
                {
                    var totalActiveListings = await this.sponsoredListingRepository.GetActiveListingsCountAsync();
                    var totalActiveReservations = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync();

                    if (CanPurchaseListing(totalActiveListings, totalActiveReservations))
                    {
                        model.CanCreateSponsoredListing = true;
                    }
                    else
                    {
                        model.Message = StringConstants.CheckoutInProcess;
                        model.CanCreateSponsoredListing = false;
                    }
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
        [HttpGet("selectlisting")]
        public async Task<IActionResult> SelectListing(
            int? subCategoryId = null,
            Guid? rsvId = null)
        {
            if (rsvId == null)
            {
                var totalActiveListings = await this.sponsoredListingRepository.GetActiveListingsCountAsync();
                var totalActiveReservations = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync();

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations))
                {
                    return this.BadRequest(new { Error = StringConstants.CheckoutInProcess });
                }

                var reservationExpirationDate = DateTime.UtcNow.AddMinutes(IntegerConstants.ReservationMinutes);
                var reservation = await this.sponsoredListingReservationRepository.CreateReservationAsync(reservationExpirationDate);
                var guid = reservation.ReservationGuid;

                // Redirect back to the same page with the new reservation ID
                return this.RedirectToAction("SelectListing", new { subCategoryId = subCategoryId, rsvId = guid });
            }
            else
            {
                var existingReservation = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value);

                if (existingReservation == null)
                {
                    return this.BadRequest(new { Error = StringConstants.ErrorWithCheckoutProcess });
                }
            }

            this.ViewBag.ReservationGuid = rsvId;

            IEnumerable<Data.Models.DirectoryEntry> entries = await this.FilterEntries(subCategoryId);

            return this.View("SelectListing", entries);
        }

        [AllowAnonymous]
        [HttpGet("selectduration")]
        public async Task<IActionResult> SelectDurationAsync(
            int id,
            Guid? rsvId = null)
        {
            var currentListings = await this.sponsoredListingRepository.GetAllActiveListingsAsync();

            if (currentListings != null)
            {
                if (!CanAdvertise(id, currentListings))
                {
                    // max listings
                    return this.BadRequest(new { Error = StringConstants.MaximumNumberOfSponsorsReached });
                }
            }

            if (rsvId == null)
            {
                var isActiveSponsor = await this.sponsoredListingRepository.IsSponsoredListingActive(id);
                var totalActiveListings = await this.sponsoredListingRepository.GetActiveListingsCountAsync();
                var totalActiveReservations = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync();

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations) && !isActiveSponsor)
                {
                    return this.BadRequest(new { Error = StringConstants.CheckoutInProcess });
                }
            }
            else
            {
                var existingReservation = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value);

                if (existingReservation == null)
                {
                    return this.BadRequest(new { Error = StringConstants.ErrorWithCheckoutProcess });
                }
            }

            this.ViewBag.ReservationGuid = rsvId;

            var model = await this.GetListingDurations();

            return this.View(model);
        }

        [AllowAnonymous]
        [HttpPost("selectduration")]
        public async Task<IActionResult> SelectDurationAsync(
            int id,
            int selectedOfferId,
            Guid? rsvId = null)
        {
            if (rsvId == null)
            {
                var isActiveSponsor = await this.sponsoredListingRepository.IsSponsoredListingActive(id);
                var totalActiveListings = await this.sponsoredListingRepository.GetActiveListingsCountAsync();
                var totalActiveReservations = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync();

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations) && !isActiveSponsor)
                {
                    return this.BadRequest(new { Error = StringConstants.CheckoutInProcess });
                }
            }
            else
            {
                var existingReservation = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value);

                if (existingReservation == null)
                {
                    return this.BadRequest(new { Error = StringConstants.ErrorWithCheckoutProcess });
                }
            }

            this.ViewBag.ReservationGuid = rsvId;

            var selectedOffer = await this.sponsoredListingOfferRepository.GetByIdAsync(selectedOfferId);

            if (selectedOffer == null)
            {
                return this.BadRequest(new { Error = "Invalid offer selection." });
            }

            var selectedDiretoryEntry = await this.directoryEntryRepository.GetByIdAsync(id);

            if (selectedDiretoryEntry == null)
            {
                return this.BadRequest(new { Error = StringConstants.DirectoryEntryNotFound });
            }

            return this.RedirectToAction(
                        "ConfirmNowPayments",
                        new
                        {
                            id = id,
                            selectedOfferId = selectedOfferId,
                            rsvId = rsvId
                        });
        }

        [AllowAnonymous]
        [HttpGet("confirmnowpayments")]
        public async Task<IActionResult> ConfirmNowPaymentsAsync(
            int id,
            int selectedOfferId,
            Guid? rsvId = null)
        {
            if (rsvId == null)
            {
                var isActiveSponsor = await this.sponsoredListingRepository.IsSponsoredListingActive(id);
                var totalActiveListings = await this.sponsoredListingRepository.GetActiveListingsCountAsync();
                var totalActiveReservations = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync();

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations) && !isActiveSponsor)
                {
                    return this.BadRequest(new { Error = StringConstants.CheckoutInProcess });
                }
            }
            else
            {
                var existingReservation = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value);

                if (existingReservation == null)
                {
                    return this.BadRequest(new { Error = StringConstants.ErrorWithCheckoutProcess });
                }
            }

            this.ViewBag.ReservationGuid = rsvId;

            var offer = await this.sponsoredListingOfferRepository.GetByIdAsync(selectedOfferId);
            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(id);

            if (offer == null || directoryEntry == null)
            {
                return this.BadRequest(new { Error = "Invalid selection." });
            }

            var link2Name = this.cacheService.GetSnippet(SiteConfigSetting.Link2Name);
            var link3Name = this.cacheService.GetSnippet(SiteConfigSetting.Link3Name);
            var currentListings = await this.sponsoredListingRepository.GetAllActiveListingsAsync();
            var viewModel = GetConfirmationModel(offer, directoryEntry, link2Name, link3Name, currentListings);

            if (currentListings != null && currentListings.Any())
            {
                viewModel.CanCreateSponsoredListing = CanAdvertise(id, currentListings);

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
            int id,
            int selectedOfferId,
            Guid? rsvId = null)
        {
            if (rsvId == null)
            {
                var isActiveSponsor = await this.sponsoredListingRepository.IsSponsoredListingActive(id);
                var totalActiveListings = await this.sponsoredListingRepository.GetActiveListingsCountAsync();
                var totalActiveReservations = await this.sponsoredListingReservationRepository.GetActiveReservationsCountAsync();

                if (!CanPurchaseListing(totalActiveListings, totalActiveReservations) && !isActiveSponsor)
                {
                    return this.BadRequest(new { Error = StringConstants.CheckoutInProcess });
                }
            }
            else
            {
                var existingReservation = await this.sponsoredListingReservationRepository.GetReservationByGuidAsync(rsvId.Value);

                if (existingReservation == null)
                {
                    return this.BadRequest(new { Error = StringConstants.ErrorWithCheckoutProcess });
                }
            }

            this.ViewBag.ReservationGuid = rsvId;

            var sponsoredListingOffer = await this.sponsoredListingOfferRepository.GetByIdAsync(selectedOfferId);

            if (sponsoredListingOffer == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvalidOfferSelection });
            }

            var directoryEntry = await this.directoryEntryRepository.GetByIdAsync(id);

            if (directoryEntry == null)
            {
                return this.BadRequest(new { Error = StringConstants.DirectoryEntryNotFound });
            }

            var existingListing = await this.sponsoredListingRepository.GetActiveListing(id);
            var startDate = DateTime.UtcNow;

            if (existingListing != null)
            {
                startDate = existingListing.CampaignEndDate;
            }

            var invoice = await this.GetInvoice(id, sponsoredListingOffer, startDate);
            var invoiceRequest = this.GetInvoiceRequest(sponsoredListingOffer, invoice);

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
                return this.BadRequest(new { Error = StringConstants.DeserializationObjectIsNull });
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
                return this.BadRequest(new { Error = StringConstants.DeserializationObjectIsNull });
            }

            if (ipnMessage.OrderId == null)
            {
                return this.BadRequest(new { Error = "Order ID is null." });
            }

            var invoice = await this.sponsoredListingInvoiceRepository
                                    .GetByInvoiceIdAsync(Guid.Parse(ipnMessage.OrderId));

            if (invoice == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            invoice.PaymentResponse = JsonConvert.SerializeObject(ipnMessage);
            invoice.PaidAmount = ipnMessage.PayAmount;
            invoice.OutcomeAmount = ipnMessage.OutcomeAmount;

            if (ipnMessage == null)
            {
                return this.BadRequest(new { Error = StringConstants.DeserializationObjectIsNull });
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

        [AllowAnonymous]
        [HttpGet("nowpaymentssuccess")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "This is the param from them")]
        public async Task<IActionResult> NowPaymentsSuccess([FromQuery] string NP_id)
        {
            var processorInvoice = await this.paymentService.GetPaymentStatusAsync(NP_id);

            if (processorInvoice == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            if (processorInvoice.OrderId == null)
            {
                return this.BadRequest(new { Error = "Order ID not found." });
            }

            var existingInvoice = await this.sponsoredListingInvoiceRepository
                                            .GetByInvoiceIdAsync(Guid.Parse(processorInvoice.OrderId));

            if (existingInvoice == null)
            {
                return this.BadRequest(new { Error = StringConstants.InvoiceNotFound });
            }

            existingInvoice.PaymentStatus = PaymentStatus.Paid;
            existingInvoice.PaymentResponse = NP_id;

            await this.CreateNewSponsoredListing(existingInvoice);

            var viewModel = new SuccessViewModel
            {
                OrderId = existingInvoice.InvoiceId,
                ListingEndDate = existingInvoice.CampaignEndDate
            };

            return this.View("NowPaymentsSuccess", viewModel);
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

        [AllowAnonymous]
        [HttpGet("offers")]
        public async Task<IActionResult> Offers()
        {
            var offers = await this.sponsoredListingOfferRepository.GetAllAsync();
            var enabledOffers = offers.Where(o => o.IsEnabled);
            return this.View(enabledOffers);
        }

        [AllowAnonymous]
        [HttpGet("current")]
        public IActionResult Current()
        {
            return this.View();
        }

        [HttpGet("activelistings")]
        public async Task<IActionResult> ActiveListings()
        {
            var listings = await this.sponsoredListingRepository.GetAllActiveListingsAsync();
            var model = new ActiveSponsoredListingViewModel
            {
                Items = listings.Select(l => new ActiveSponsoredListingModel
                {
                    ListingName = l.DirectoryEntry?.Name ?? StringConstants.DefaultName,
                    SponsoredListingId = l.SponsoredListingId,
                    CampaignEndDate = l.CampaignEndDate,
                    ListingUrl = l.DirectoryEntry?.Link ?? string.Empty,
                    DirectoryListingId = l.DirectoryEntryId
                }).ToList()
            };

            return this.View("activelistings", model);
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

        private static ConfirmSelectionViewModel GetConfirmationModel(
            SponsoredListingOffer offer,
            Data.Models.DirectoryEntry directoryEntry,
            string link2Name,
            string link3Name,
            IEnumerable<SponsoredListing> currentListings)
        {
            return new ConfirmSelectionViewModel
            {
                SelectedDirectoryEntry = new DirectoryEntryViewModel()
                {
                    DirectoryEntry = directoryEntry,
                    Link2Name = link2Name,
                    Link3Name = link3Name
                },
                Offer = new SponsoredListingOfferModel()
                {
                    Description = offer.Description,
                    Days = offer.Days,
                    Id = offer.Id,
                    USDPrice = offer.Price
                },
                IsExtension = currentListings.FirstOrDefault(x => x.DirectoryEntryId == directoryEntry.DirectoryEntryId) != null
            };
        }

        private static PaymentStatus ConvertToInternalStatus(
            NowPayments.API.Enums.PaymentStatus externalStatus)
        {
            return externalStatus switch
            {
                NowPayments.API.Enums.PaymentStatus.Unknown => PaymentStatus.Unknown,
                NowPayments.API.Enums.PaymentStatus.Waiting => PaymentStatus.InvoiceCreated,
                NowPayments.API.Enums.PaymentStatus.Sending or
                    NowPayments.API.Enums.PaymentStatus.Confirming or
                    NowPayments.API.Enums.PaymentStatus.Confirmed => PaymentStatus.Pending,
                NowPayments.API.Enums.PaymentStatus.Finished => PaymentStatus.Paid,
                NowPayments.API.Enums.PaymentStatus.PartiallyPaid => PaymentStatus.UnderPayment,
                NowPayments.API.Enums.PaymentStatus.Failed or
                    NowPayments.API.Enums.PaymentStatus.Refunded => PaymentStatus.Failed,
                NowPayments.API.Enums.PaymentStatus.Expired => PaymentStatus.Expired,
                _ => throw new ArgumentOutOfRangeException(nameof(externalStatus), externalStatus, null),
            };
        }

        private static bool CanAdvertise(
            int directoryEntryId,
            IEnumerable<SponsoredListing> currentListings)
        {
            if (currentListings.FirstOrDefault(x => x.DirectoryEntryId == directoryEntryId) != null)
            {
                return true;
            }

            return currentListings.Count() < IntegerConstants.MaxSponsoredListings;
        }

        private static bool CanPurchaseListing(int totalActiveListings, int totalActiveReservations)
        {
            return (totalActiveListings <= IntegerConstants.MaxSponsoredListings) &&
                   (totalActiveReservations < (IntegerConstants.MaxSponsoredListings - totalActiveListings));
        }

        private PaymentRequest GetInvoiceRequest(SponsoredListingOffer sponsoredListingOffer, SponsoredListingInvoice invoice)
        {
            return new PaymentRequest
            {
                IsFeePaidByUser = true,
                PriceAmount = sponsoredListingOffer.Price,
                PriceCurrency = this.paymentService.PriceCurrency,
                PayCurrency = this.paymentService.PayCurrency,
                OrderId = invoice.InvoiceId.ToString(),
                OrderDescription = sponsoredListingOffer.Description
            };
        }

        private async Task<SponsoredListingInvoice> GetInvoice(int directoryEntryId, SponsoredListingOffer sponsoredListingOffer, DateTime startDate)
        {
            return await this.sponsoredListingInvoiceRepository.CreateAsync(
                new SponsoredListingInvoice
                {
                    DirectoryEntryId = directoryEntryId,
                    Currency = Currency.USD,
                    InvoiceId = Guid.NewGuid(),
                    PaymentStatus = PaymentStatus.InvoiceCreated,
                    CampaignStartDate = startDate,
                    CampaignEndDate = startDate.AddDays(sponsoredListingOffer.Days),
                    Amount = sponsoredListingOffer.Price,
                    InvoiceDescription = sponsoredListingOffer.Description
                });
        }

        private async Task<List<SponsoredListingOfferModel>> GetListingDurations()
        {
            var offers = await this.sponsoredListingOfferRepository.GetAllAsync();
            var model = new List<SponsoredListingOfferModel>();

            foreach (var offer in offers.OrderBy(x => x.Days))
            {
                model.Add(new SponsoredListingOfferModel
                {
                    Id = offer.Id,
                    Description = offer.Description,
                    Days = offer.Days,
                    USDPrice = offer.Price
                });
            }

            return model;
        }

        private async Task CreateNewSponsoredListing(SponsoredListingInvoice invoice)
        {
            if (await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice))
            {
                var existingSponsoredListing = await this.sponsoredListingRepository
                                                         .GetByInvoiceIdAsync(invoice.SponsoredListingInvoiceId);

                if (existingSponsoredListing == null && invoice.PaymentStatus == PaymentStatus.Paid)
                {
                    var activeListing = await this.sponsoredListingRepository
                                                  .GetActiveListing(invoice.DirectoryEntryId);

                    if (activeListing == null)
                    {
                        // if the invoice is paid and there is no existing sponsored listing, create one
                        var sponsoredListing = await this.sponsoredListingRepository.CreateAsync(
                            new SponsoredListing()
                            {
                                DirectoryEntryId = invoice.DirectoryEntryId,
                                CampaignStartDate = invoice.CampaignStartDate,
                                CampaignEndDate = invoice.CampaignEndDate,
                                SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId,
                            });

                        invoice.SponsoredListingId = sponsoredListing.SponsoredListingId;
                        await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);
                    }
                    else
                    {
                        // extend the existing listing
                        activeListing.CampaignEndDate = invoice.CampaignEndDate;

                        // set the current active listing's invoice to the new invoice
                        activeListing.SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId;
                        await this.sponsoredListingRepository.UpdateAsync(activeListing);

                        // set the new invoice's sponsored listing to the current active listing
                        invoice.SponsoredListingId = activeListing.SponsoredListingId;
                        await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);
                    }

                    this.ClearCachedItems();
                }
            }
        }

        private async Task<IEnumerable<Data.Models.DirectoryEntry>> FilterEntries(int? subCategoryId)
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
            return entries;
        }
    }
}
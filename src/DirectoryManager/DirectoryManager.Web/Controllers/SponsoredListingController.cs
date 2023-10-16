using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NowPayments.API.Constants;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;

namespace DirectoryManager.Web.Controllers
{
    [Route("sponsoredlistings")]
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
            : base(trafficLogRepository, userAgentCacheService)
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
        public IActionResult Index()
        {
            return this.View();
        }

        [AllowAnonymous]
        [HttpGet("selectlisting")]
        public async Task<IActionResult> SelectListing(int? subCategoryId = null)
        {
            var entries = await this.directoryEntryRepository.GetAllAsync();

            if (subCategoryId.HasValue)
            {
                entries = entries.Where(e => e.SubCategory?.Id == subCategoryId.Value).ToList();
            }

            entries = entries.OrderBy(e => e.Name)
                             .ToList();

            this.ViewBag.SubCategories = (await this.subCategoryRepository.GetAllAsync())
                                    .OrderBy(sc => sc.Category.Name)
                                    .ThenBy(sc => sc.Name)
                                    .ToList();

            return this.View("SelectListing", entries);
        }

        [AllowAnonymous]
        [HttpGet("success")]
        public async Task<IActionResult> Success([FromQuery] string NP_id)
        {
            var processorInvoice = await this.paymentService.GetPaymentStatusAsync(NP_id);

            var existingInvoice = await this.sponsoredListingInvoiceRepository
                                        .GetByInvoiceIdAsync(Guid.Parse(processorInvoice.OrderId));

            if (existingInvoice == null)
            {
                return this.BadRequest(new { Error = "Invoice not found." });
            }

            // todo: determine if this means it's paid and if so, create the sponsored listing

            await this.sponsoredListingInvoiceRepository.UpdateAsync(existingInvoice);

            return this.View("success");
        }

        [AllowAnonymous]
        [HttpGet("confirmselection")]
        public async Task<IActionResult> ConfirmSelectionAsync(
            int id,
            SponsoredListingOffers offerSelection)
        {
            var sponsoredListingOffer = this.sponsoredListings
                                            .SponsoredListingOffers
                                            .FirstOrDefault(x => x.Key.ToLower() == offerSelection.ToString().ToLower());

            if (sponsoredListingOffer == null)
            {
                return this.BadRequest(new { Error = "Invalid offer selection." });
            }

            var campaignDurationInDays = this.GetCampaignDuration(offerSelection);
            var now = DateTime.UtcNow;

            var invoice = await this.sponsoredListingInvoiceRepository.CreateAsync(
                new SponsoredListingInvoice
                {
                    DirectoryEntryId = id,
                    Currency = Currency.USD,
                    InvoiceId = Guid.NewGuid(),
                    PaymentStatus = PaymentStatus.InvoiceCreated,
                    CampaignStartDate = now,
                    CampaignEndDate = now.AddDays(campaignDurationInDays),
                    Amount = sponsoredListingOffer.USDPrice,
                    InvoiceDescription = sponsoredListingOffer.Description
                });

            var processorInvoice = await this.paymentService.CreateInvoice(
                new PaymentRequest
                {
                    IsFeePaidByUser = true,
                    PriceAmount = sponsoredListingOffer.USDPrice,
                    PriceCurrency = Currency.USD.ToString(),
                    PayCurrency = Currency.XMR.ToString(),
                    OrderId = invoice.InvoiceId.ToString(),
                    OrderDescription = sponsoredListingOffer.Description
                });

            invoice.ProcessorInvoiceId = processorInvoice.Id;
            invoice.PaymentProcessor = PaymentProcessor.NOWPayments;
            invoice.InvoiceResponse = JsonConvert.SerializeObject(processorInvoice);

            await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice);

            return this.Redirect(processorInvoice.InvoiceUrl);
        }

        [AllowAnonymous]
        [HttpPost("callback")]
        public async Task<IActionResult> CallBackAsync()
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

            var nowPaymentsSig = this.Request.Headers[StringConstants.HeaderNameAuthCallBack].FirstOrDefault() ?? string.Empty;

            bool isValidRequest = this.paymentService.IsIpnRequestValid(
                callbackPayload,
                nowPaymentsSig,
                out string errorMsg);

            if (!isValidRequest)
            {
                return this.BadRequest(new { Error = errorMsg });
            }

            var invoice = await this.sponsoredListingInvoiceRepository
                                    .GetByInvoiceIdAsync(Guid.Parse(ipnMessage.OrderId));

            if (invoice == null)
            {
                return this.BadRequest(new { Error = "Invoice not found." });
            }

            invoice.InvoiceResponse = callbackPayload;
            invoice.PaymentResponse = JsonConvert.SerializeObject(ipnMessage);
            invoice.Amount = ipnMessage.PayAmount;

            var processorPaymentStatus = EnumHelper.ParseStringToEnum<NowPayments.API.Enums.PaymentStatus>(ipnMessage.PaymentStatus);
            var translatedValue = ConvertToInternalStatus(processorPaymentStatus);
            invoice.PaymentStatus = translatedValue;

            var processorCurrency = EnumHelper.ParseStringToEnum<Currency>(ipnMessage.PayCurrency);
            invoice.Currency = processorCurrency;

            if (await this.sponsoredListingInvoiceRepository.UpdateAsync(invoice))
            {
                var existingSponsoredListing = await this.sponsoredListingRepository
                                                    .GetByInvoiceIdAsync(invoice.SponsoredListingInvoiceId);

                if (existingSponsoredListing == null)
                {
                    await this.sponsoredListingRepository.CreateAsync(
                        new SponsoredListing()
                        {
                            DirectoryEntryId = invoice.DirectoryEntryId,
                            CampaignStartDate = invoice.CampaignStartDate,
                            CampaignEndDate = invoice.CampaignEndDate,
                            SponsoredListingInvoiceId = invoice.SponsoredListingInvoiceId,
                        });
                }
            }

            return this.Ok();
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


        private int GetCampaignDuration(SponsoredListingOffers offerSelection)
        {
            switch (offerSelection)
            {
                case SponsoredListingOffers.SevenDays:
                    return 7;
                case SponsoredListingOffers.ThirtyDays:
                    return 30;
                case SponsoredListingOffers.NinetyDays:
                    return 90;
                default:
                    throw new ArgumentOutOfRangeException(nameof(offerSelection), offerSelection, null);
            }
        }
    }
}
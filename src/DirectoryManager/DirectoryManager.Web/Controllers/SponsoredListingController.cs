using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NowPayments.API.Implementations;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;
using System.Text;

namespace DirectoryManager.Web.Controllers
{
    public class SponsoredListingController : BaseController
    {
        private readonly IDirectoryEntryRepository directoryEntryRepository;
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository;
        private readonly IPaymentService paymentService;
        private readonly IMemoryCache cache;

        public SponsoredListingController(
            IDirectoryEntryRepository directoryEntryRepository,
            ISponsoredListingRepository sponsoredListingRepository,
            ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
            ITrafficLogRepository trafficLogRepository,
            IPaymentService paymentService,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService)
        {
            this.directoryEntryRepository = directoryEntryRepository;
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.sponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
            this.paymentService = paymentService;
            this.cache = cache;
        }

        [HttpGet("sponsored-listing")]
        public IActionResult Index()
        {
            var result = this.paymentService.CreateInvoice(new PaymentRequest
            {
                IsFeePaidByUser = true,
                PriceAmount = 20,
                PriceCurrency = Currency.USD.ToString(),
                PayCurrency = Currency.XMR.ToString(),
                OrderId = Guid.NewGuid().ToString(),
                OrderDescription = "Sponsored Listing",
                SuccessUrl = "https://monerica.com",
                CancelUrl = "https://monerica.com",
                IpnCallbackUrl = "https://monerica.com/sponsored-listing/callback"
            });
            return this.View();
        }

        [HttpPost("sponsored-listing/callback")]
        public async Task<IActionResult> CallBackAsync()
        {
            using var reader = new StreamReader(this.Request.Body, Encoding.UTF8);
            var callbackPayload = await reader.ReadToEndAsync();

            bool isValidRequest = this.paymentService.IsIpnRequestValid(
                callbackPayload,
                this.Request.Headers["HTTP_X_NOWPAYMENTS_SIG"].ToString(),
                out string errorMsg);

            if (!isValidRequest)
            {
                return this.BadRequest(new { Error = errorMsg });
            }

            return this.Ok();
        }
    }
}
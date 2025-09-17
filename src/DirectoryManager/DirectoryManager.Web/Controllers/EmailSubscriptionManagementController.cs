using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class EmailSubscriptionManagementController : Controller
    {
        private readonly IEmailSubscriptionRepository emailSubscriptionRepository;

        public EmailSubscriptionManagementController(
            IEmailSubscriptionRepository emailSubscriptionRepository)
        {
            this.emailSubscriptionRepository = emailSubscriptionRepository;
        }

        [Route("account/emailsubscriptionmanagement")]
        public IActionResult Index(int page = 1, int pageSize = IntegerConstants.DefaultPageSize)
        {
            int totalItems; // To capture the total number of items in the repository
            var pagedEmails = this.emailSubscriptionRepository.GetPagedDescending(page, pageSize, out totalItems);

            var model = new PagedEmailSubscribeEditListModel
            {
                TotalSubscribed = this.emailSubscriptionRepository.Total(),
                Items = pagedEmails.Select(sub => new EmailSubscribeEditModel
                {
                    Email = sub.Email,
                    IsSubscribed = sub.IsSubscribed,
                    EmailSubscriptionId = sub.EmailSubscriptionId,
                    CreateDate = sub.CreateDate,
                    IpAddress = sub.IpAddress
                }).ToList(),
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return this.View(model);
        }

        [Route("account/emailsubscriptionmanagement/edit")]
        [HttpPost]
        public IActionResult Edit(EmailSubscribeEditModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var dbModel = this.emailSubscriptionRepository.Get(model.EmailSubscriptionId);

            if (dbModel == null)
            {
                return this.NotFound();
            }

            dbModel.Email = model.Email;
            dbModel.IsSubscribed = model.IsSubscribed;

            this.emailSubscriptionRepository.Update(dbModel);

            return this.RedirectToAction("index");
        }

        [Route("account/emailsubscriptionmanagement/edit")]
        [HttpGet]
        public IActionResult Edit(int emailSubscriptionId)
        {
            var dbModel = this.emailSubscriptionRepository.Get(emailSubscriptionId);

            if (dbModel == null)
            {
                return this.NotFound();
            }

            var model = new EmailSubscribeEditModel()
            {
                Email = dbModel.Email,
                IsSubscribed = dbModel.IsSubscribed,
                EmailSubscriptionId = dbModel.EmailSubscriptionId
            };

            return this.View(model);
        }

        [Route("account/emailsubscriptionmanagement/upload")]
        [HttpGet]
        public IActionResult Upload()
        {
            return this.View();
        }

        [Route("account/emailsubscriptionmanagement/upload")]
        [HttpPost]
        public IActionResult Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                this.ModelState.AddModelError(string.Empty, "Please upload a valid CSV file.");
                return this.View();
            }

            try
            {
                using var streamReader = new StreamReader(file.OpenReadStream());
                using var csvReader = new CsvReader(streamReader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ",",
                    PrepareHeaderForMatch = args => args.Header.ToLower() // Normalize headers to lowercase
                });

                csvReader.Context.RegisterClassMap<EmailBounceMap>();

                var bounces = csvReader.GetRecords<EmailBounce>().ToList();

                foreach (var bounce in bounces)
                {
                    var subscription = this.emailSubscriptionRepository.Get(bounce.Email);
                    if (subscription != null)
                    {
                        subscription.IsSubscribed = false;
                        this.emailSubscriptionRepository.Update(subscription);
                    }
                }

                this.TempData[StringConstants.SuccessMessage] = "Unsubscribed emails successfully.";
            }
            catch (Exception ex)
            {
                this.ModelState.AddModelError(string.Empty, $"An error occurred while processing the file: {ex.Message}");
            }

            return this.View();
        }
    }
}
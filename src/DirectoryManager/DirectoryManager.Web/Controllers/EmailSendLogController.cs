using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Controllers
{
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public class EmailSendLogController : Controller
    {
        public const string ResultSent = "sent";
        public const string ResultFailed = "failed";

        private const int MaxPageSize = 200;

        private readonly IEmailSendLogRepository emailSendLogRepository;
        private readonly IOptionsMonitor<EmailSendLogOptions> options;

        public EmailSendLogController(
            IEmailSendLogRepository emailSendLogRepository,
            IOptionsMonitor<EmailSendLogOptions> options)
        {
            this.emailSendLogRepository = emailSendLogRepository;
            this.options = options;
        }

        [Route("email/sendlog")]
        public IActionResult Index(
            string? source = null,
            int page = 1,
            int pageSize = 25,
            string? recipient = null,
            string? subject = null,
            string? result = null,
            int? contentId = null)
        {
            const int MinPage = 1;
            page = Math.Max(page, MinPage);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var pageIndex = page - 1; // zero-based

            result = result?.Trim().ToLowerInvariant();
            if (result != ResultSent && result != ResultFailed)
            {
                result = null;
            }

            var filter = new EmailSendLogFilter
            {
                Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
                RecipientContains = string.IsNullOrWhiteSpace(recipient) ? null : recipient.Trim(),
                SubjectContains = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
                IsSuccess = result == null ? null : result == ResultSent,
                ContentId = contentId is > 0 ? contentId : null,
            };

            IList<EmailSendLog> records = this.emailSendLogRepository.Search(filter, pageIndex, pageSize, out var totalRecords);

            var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            this.ViewBag.CurrentPage = page;
            this.ViewBag.TotalPages = totalPages;
            this.ViewBag.TotalRecords = totalRecords;
            this.ViewBag.Source = filter.Source;
            this.ViewBag.Recipient = filter.RecipientContains;
            this.ViewBag.Subject = filter.SubjectContains;
            this.ViewBag.Result = result;
            this.ViewBag.ContentId = filter.ContentId;
            this.ViewBag.PageSize = pageSize;

            return this.View(records);
        }

        [Route("email/sendlog/{id:int}")]
        public IActionResult Details(int id)
        {
            var log = this.emailSendLogRepository.GetWithContent(id);
            if (log == null)
            {
                return this.NotFound();
            }

            var others = log.EmailSendLogContentId.HasValue
                ? Math.Max(0, this.emailSendLogRepository.CountByContent(log.EmailSendLogContentId.Value) - 1)
                : 0;

            return this.View(new EmailSendLogDetailViewModel
            {
                Log = log,
                Content = log.Content,
                OtherSendsWithSameContent = others,
                RetentionDays = this.options.CurrentValue.EffectiveRetentionDays,
            });
        }
    }
}

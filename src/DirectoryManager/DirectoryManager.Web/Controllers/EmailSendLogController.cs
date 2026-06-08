using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class EmailSendLogController : Controller
    {
        private readonly IEmailSendLogRepository emailSendLogRepository;

        public EmailSendLogController(IEmailSendLogRepository emailSendLogRepository)
        {
            this.emailSendLogRepository = emailSendLogRepository;
        }

        [Route("email/sendlog")]
        public IActionResult Index(string? source = null, int page = 1, int pageSize = 25)
        {
            const int MinPage = 1;
            page = Math.Max(page, MinPage);

            var pageIndex = page - 1; // zero-based

            int totalRecords;
            IList<EmailSendLog> records;

            if (string.IsNullOrWhiteSpace(source))
            {
                records = this.emailSendLogRepository.GetPagedRecords(pageIndex, pageSize, out totalRecords);
            }
            else
            {
                records = this.emailSendLogRepository.GetBySource(source, pageIndex, pageSize, out totalRecords);
            }

            var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            this.ViewBag.CurrentPage = page;
            this.ViewBag.TotalPages = totalPages;
            this.ViewBag.TotalRecords = totalRecords;
            this.ViewBag.Source = source;
            this.ViewBag.PageSize = pageSize;

            return this.View(records);
        }
    }
}

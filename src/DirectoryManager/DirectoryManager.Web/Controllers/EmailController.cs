using DirectoryManager.Services.Interfaces;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class EmailController : Controller
    {
        private readonly IEmailService emailService;

        public EmailController(IEmailService emailService)
        {
            this.emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        }

        [HttpGet("email/testemail")]
        public IActionResult TestEmail()
        {
            var model = new TestEmailViewModel();
            return this.View(model);
        }

        [HttpPost("email/testemail")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestEmail(TestEmailViewModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            try
            {
                // Send the test email
                await this.emailService.SendEmailAsync(
                    model.Subject,
                    model.BodyText,
                    model.BodyHtml,
                    new List<string> { model.RecipientEmail });

                this.TempData["SuccessMessage"] = "Test email sent successfully.";
            }
            catch (Exception ex)
            {
                this.TempData["ErrorMessage"] = $"Failed to send test email: {ex.Message}";
            }

            return this.RedirectToAction(nameof(this.TestEmail));
        }
    }
}
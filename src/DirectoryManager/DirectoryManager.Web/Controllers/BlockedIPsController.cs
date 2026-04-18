using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class BlockedIPsController : Controller
    {
        private readonly IBlockedIPRepository blockedIPRepository;

        public BlockedIPsController(IBlockedIPRepository blockedIPRepository)
        {
            this.blockedIPRepository = blockedIPRepository;
        }

        [Route("blockedips/index")]
        public async Task<IActionResult> Index()
        {
            var blockedIPs = await this.blockedIPRepository.GetAllAsync();
            return this.View(blockedIPs);
        }

        [Route("blockedips/create")]
        public IActionResult Create()
        {
            return this.View();
        }

        [Route("blockedips/create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlockedIP blockedIP)
        {
            if (this.ModelState.IsValid)
            {
                await this.blockedIPRepository.CreateAsync(blockedIP);
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(blockedIP);
        }

        [Route("blockedips/delete")]
        public async Task<IActionResult> Delete(int id)
        {
            await this.blockedIPRepository.DeleteAsync(id);
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
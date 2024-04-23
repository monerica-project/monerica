using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class ErrorsController : Controller
    {

        public ErrorsController()
        {
        }

        [Route("errors/404")]
        [HttpGet]
        public IActionResult Page404()
        {
            return this.View("404");
        }
    }
}
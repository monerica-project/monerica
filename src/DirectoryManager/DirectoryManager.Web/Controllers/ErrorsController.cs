using DirectoryManager.Web.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class ErrorsController : Controller
    {
        public ErrorsController()
        {
        }

        /// <summary>
        /// The error page. Reached by re-execution from UseStatusCodePagesWithReExecute (the
        /// browser stays on the URL it asked for and gets the real status), or directly. Any HTTP
        /// method is accepted, because a re-executed request keeps its original method (a POST to
        /// a missing URL must still get the page, not a 405).
        /// </summary>
        [Route("errors/{code:int}")]
        public IActionResult Error(int code)
        {
            var status = code is >= 400 and <= 599 ? code : 404;
            this.Response.StatusCode = status;

            this.PageMeta().Clear();

            return this.View(status == 404 ? "404" : "Status", status);
        }
    }
}

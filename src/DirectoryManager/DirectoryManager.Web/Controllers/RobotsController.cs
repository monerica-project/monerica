using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class RobotsController : Controller
    {
        public RobotsController()
        {
        }

        [Route("robots.txt")]
        [HttpGet]
        public ContentResult RobotsTxt()
        {
            var sb = new StringBuilder();

            sb.AppendLine("User-agent: *");
            sb.AppendLine($"Allow: /");

            return this.Content(sb.ToString());
        }
    }
}
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.ContentSnippet;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class ContentSnippetManagementController : BaseController
    {
        private readonly IContentSnippetRepository contentSnippetRepository;
        private readonly ICacheService contentSnippetHelper;

        public ContentSnippetManagementController(
            IContentSnippetRepository contentSnippetRepository,
            ICacheService contentSnippetHelper,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.contentSnippetRepository = contentSnippetRepository;
            this.contentSnippetHelper = contentSnippetHelper;
        }

        [Route("ContentSnippetManagement")]
        public IActionResult Index()
        {
            var allSnippets = this.contentSnippetRepository.GetAll().OrderBy(x => x.SnippetType.ToString());
            var model = new ContentSnippetEditListModel();

            foreach (var snippet in allSnippets)
            {
                model.Items.Add(new ContentSnippetEditModel()
                {
                    Content = snippet.Content,
                    ContentSnippetId = snippet.ContentSnippetId,
                    SnippetType = snippet.SnippetType
                });
            }

            return this.View(model);
        }

        [Route("ContentSnippetManagement/create")]
        [HttpPost]
        public IActionResult Create(ContentSnippetEditModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var dbModel = this.contentSnippetRepository.Get(model.ContentSnippetId);

            if (dbModel != null)
            {
                throw new Exception("type already exists");
            }

            this.contentSnippetRepository.Create(new ContentSnippet()
            {
                Content = model.Content?.Trim(),
                ContentSnippetId = model.ContentSnippetId,
                SnippetType = model.SnippetType
            });

            return this.RedirectToAction("index");
        }

        [Route("ContentSnippetManagement/create")]
        [HttpGet]
        public IActionResult Create()
        {
            var existingSnippets = this.contentSnippetRepository.GetAll().OrderBy(x => x.SnippetType.ToString());

            var model = new ContentSnippetEditModel()
            {
                SnippetType = Data.Enums.SiteConfigSetting.Unknown
            };

            foreach (string snippetType in Enum.GetNames(typeof(Data.Enums.SiteConfigSetting)))
            {
                if (existingSnippets.FirstOrDefault(x => x.SnippetType.ToString() == snippetType) != null)
                {
                    continue;
                }

                model.UnusedSnippetTypes.Add(new SelectListItem()
                {
                    Text = snippetType.ToString(),
                    Value = snippetType.ToString(),
                });
            }

            model.UnusedSnippetTypes = model.UnusedSnippetTypes.OrderBy(x => x.Text).ToList();

            return this.View(model);
        }

        [Route("ContentSnippetManagement/edit")]
        [HttpPost]
        public IActionResult Edit(ContentSnippetEditModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            if (model.Content == null)
            {
                model.Content = string.Empty;
            }

            var dbModel = this.contentSnippetRepository.Get(model.ContentSnippetId);

            if (dbModel == null)
            {
                throw new Exception("Content Snippet does not exist");
            }

            dbModel.Content = model.Content.Trim();
            dbModel.SnippetType = model.SnippetType;

            this.contentSnippetRepository.Update(dbModel);

            this.contentSnippetHelper.ClearSnippetCache(model.SnippetType);

            this.ClearCachedItems();

            return this.RedirectToAction("index");
        }

        [Route("ContentSnippetManagement/edit")]
        [HttpGet]
        public IActionResult Edit(int contentSnippetId)
        {
            var dbModel = this.contentSnippetRepository.Get(contentSnippetId);

            if (dbModel == null)
            {
                throw new Exception("Content Snippet does not exist");
            }

            var model = new ContentSnippetEditModel()
            {
                Content = dbModel.Content,
                ContentSnippetId = dbModel.ContentSnippetId,
                SnippetType = dbModel.SnippetType,
            };

            return this.View(model);
        }

        [Route("ContentSnippetManagement/delete")]
        [HttpPost]
        public IActionResult Delete(int contentSnippetId)
        {
            this.contentSnippetRepository.Delete(contentSnippetId);

            return this.RedirectToAction("index");
        }
    }
}
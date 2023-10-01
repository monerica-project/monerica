using DirectoryManager.Web.Constants;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.TagHelpers
{
    [HtmlTargetElement("test")]
    public class TestTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.Content.SetContent("This is a test.");
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Services.Interfaces
{
    public interface IUserContentModerationService
    {
        Task<UserContentModerationResult> EvaluateReviewAsync(string? body, CancellationToken ct);
        Task<UserContentModerationResult> EvaluateReplyAsync(string? body, CancellationToken ct);
    }
}

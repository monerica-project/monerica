using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Web.Services.Interfaces
{
    public interface ISponsorTickerService
    {
        Task<List<SponsorTickerItemVm>> GetItemsAsync();
    }
}

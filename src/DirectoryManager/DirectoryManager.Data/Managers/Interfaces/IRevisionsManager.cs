using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Managers.Interfaces
{
    public interface IRevisionsManager
    {
        public DateTime GetLastRevisionDate();

        public Task<IEnumerable<DirectoryEntry>> GetRevisionsInRange(DateTime start, DateTime end);
    }
}

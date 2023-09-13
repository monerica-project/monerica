using DirectoryManager.Data.Managers.Interfaces;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Managers.Implementations
{
    public class RevisionsManager : IRevisionsManager
    {
        public DateTime GetLastRevisionDate()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<DirectoryEntry>> GetRevisionsInRange(DateTime start, DateTime end)
        {
            throw new NotImplementedException();
        }
    }
}

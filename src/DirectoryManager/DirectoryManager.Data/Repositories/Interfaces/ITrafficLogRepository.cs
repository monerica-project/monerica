using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ITrafficLogRepository
    {
        Task AddTrafficLog(TrafficLog trafficLog);
        void DeleteTrafficLog(TrafficLog trafficLog);
        int GetUniqueIpsInRange(DateTime start, DateTime end);
        int GetTotalLogsInRange(DateTime start, DateTime end);
    }
}
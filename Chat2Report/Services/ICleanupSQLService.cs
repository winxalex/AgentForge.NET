using Chat2Report.Models;

namespace Chat2Report.Services
{
    public interface ICleanupSQLService
    {
        string Clean(string rawSql, UserQueryAnalysis? analysis);
    }
}

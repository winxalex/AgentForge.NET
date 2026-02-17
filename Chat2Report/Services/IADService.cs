using Chat2Report.Models;

namespace Chat2Report.Services
{
    public interface IADService
    {
        // Додаден е нов опционален параметар groupName
        Task<List<UserModel>> FindUsersByNameAsync(string? firstName, string? lastName, string? groupName = null);
    }
}
using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResult> Login(string username, string password);
        string HashPassword(string password);
    }
}

namespace ThriveWellness.Services.Interfaces
{
    public interface IAuthService
    {
        Task<bool> Login(string username, string password);
        string HashPassword(string password);
    }
}

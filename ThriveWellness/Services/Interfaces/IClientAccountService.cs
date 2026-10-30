using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IClientAccountService
    {
        // Turns a completed Google sign-in into the matching Client row:
        // found by Google subject, else linked by email, else created.
        Task<ClientSignInResult> SignInWithGoogleAsync(GoogleProfile profile);
    }
}

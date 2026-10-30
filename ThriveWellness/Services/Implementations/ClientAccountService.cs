using Microsoft.EntityFrameworkCore;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: decides which Client a Google sign-in belongs to.
    //
    // 1. The email must be verified by Google - otherwise anyone could
    //    create a Google account with someone else's address and inherit
    //    their booking history.
    // 2. A client already linked to this Google account (GoogleSubject) is
    //    simply signed in.
    // 3. Otherwise an existing client with the same email (ignoring case)
    //    is linked by saving the GoogleSubject - this is how clients who
    //    booked before sign-in existed keep their history.
    // 4. Otherwise a new client is created from the Google name and email.
    public class ClientAccountService : IClientAccountService
    {
        private readonly IClientRepository _clientRepository;
        private readonly ILogger<ClientAccountService> _logger;

        public ClientAccountService(IClientRepository clientRepository, ILogger<ClientAccountService> logger)
        {
            _clientRepository = clientRepository;
            _logger = logger;
        }

        public async Task<ClientSignInResult> SignInWithGoogleAsync(GoogleProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.Subject) || string.IsNullOrWhiteSpace(profile.Email))
            {
                return Fail("Google didn't share an email address for this account, so we can't sign you in.");
            }

            if (!profile.EmailVerified)
            {
                return Fail("Your Google account's email address isn't verified yet. Please verify it with Google and try again.");
            }

            var linked = await _clientRepository.GetByGoogleSubjectAsync(profile.Subject);
            if (linked != null)
            {
                return Succeed(linked);
            }

            var byEmail = await _clientRepository.GetByEmailIgnoringCaseAsync(profile.Email);
            if (byEmail != null)
            {
                if (byEmail.GoogleSubject != null)
                {
                    // Already linked to a different Google account - never
                    // silently move a client's history between accounts.
                    _logger.LogWarning("Client {ClientId} is already linked to a different Google account.", byEmail.ClientId);
                    return Fail("This email address is already linked to a different Google account. Please contact the studio.");
                }

                byEmail.GoogleSubject = profile.Subject;
                await _clientRepository.UpdateAsync(byEmail);
                return Succeed(byEmail);
            }

            var client = new Client
            {
                GoogleSubject = profile.Subject,
                FullName = string.IsNullOrWhiteSpace(profile.Name) ? profile.Email.Split('@')[0] : profile.Name.Trim(),
                Email = profile.Email.Trim(),
                PhoneNumber = string.Empty,
                IsNew = true,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _clientRepository.AddAsync(client);
            }
            catch (DbUpdateException)
            {
                // Two sign-ins for the same new account racing each other:
                // the unique GoogleSubject index stops the second insert, so
                // use the row the first one created.
                var winner = await _clientRepository.GetByGoogleSubjectAsync(profile.Subject);
                if (winner == null)
                {
                    throw;
                }
                return Succeed(winner);
            }

            return Succeed(client);
        }

        private static ClientSignInResult Succeed(Client client) => new() { Success = true, Client = client };

        private static ClientSignInResult Fail(string message) => new() { Success = false, ErrorMessage = message };
    }
}

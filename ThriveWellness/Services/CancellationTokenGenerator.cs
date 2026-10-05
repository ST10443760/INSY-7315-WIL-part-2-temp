using System.Security.Cryptography;

namespace ThriveWellness.Services
{
    // Generates the random token used in cancellation links (FR-18) - a
    // booking's confirmation and waitlist-promotion emails both carry this
    // token in a URL (/Booking/Cancel/{token}), and it's the only thing that
    // proves whoever clicks it is allowed to cancel that specific booking,
    // since clients never log in.
    public static class CancellationTokenGenerator
    {
        // Cryptographically random (RandomNumberGenerator, not Random or a
        // sequential id) so a token can't be guessed or brute-forced from
        // another one - with no login gating this endpoint, the token itself
        // is the entire security boundary. Base64url-encoded (+ and / swapped
        // for - and _, padding stripped) so it drops straight into a URL path
        // segment with no further escaping needed.
        //
        // "Single use" isn't enforced by invalidating the token itself; it
        // falls out of what the token points at - once used, the booking's
        // Status is Cancelled, and BookingService.CancelAsync refuses to
        // cancel an already-cancelled booking, so reusing the same link again
        // just reports "already cancelled" instead of doing anything.
        public static string Generate()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}

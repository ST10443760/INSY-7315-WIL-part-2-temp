namespace ThriveWellness.Models
{
    // Return value of AuthService.Login. Deliberately has no "unknown
    // username" member: an unknown username comes back as
    // InvalidCredentials (or LockedOut after repeated tries), exactly like a
    // real one, so the caller can never tell the two apart.
    public enum LoginResult
    {
        Success,
        InvalidCredentials,
        LockedOut
    }
}

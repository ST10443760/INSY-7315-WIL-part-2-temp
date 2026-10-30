using System.Security.Claims;

namespace ThriveWellness.Authorization
{
    // Builds and reads the claims on a signed-in client's cookie, so the
    // claim names live in one place.
    public static class ClientClaims
    {
        public static List<Claim> For(int clientId, string fullName, string email) => new()
        {
            new Claim(AuthPolicies.ClientIdClaim, clientId.ToString()),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, AuthPolicies.ClientRole)
        };

        public static bool IsClient(this ClaimsPrincipal user) =>
            user.Identity?.IsAuthenticated == true && user.IsInRole(AuthPolicies.ClientRole);

        // Null for anyone who isn't a signed-in client.
        public static int? GetClientId(this ClaimsPrincipal user) =>
            user.IsClient() && int.TryParse(user.FindFirstValue(AuthPolicies.ClientIdClaim), out var id) ? id : null;

        // "Jane" from "Jane Doe", for the dashboard greeting.
        public static string FirstName(this ClaimsPrincipal user)
        {
            var name = user.Identity?.Name?.Trim() ?? string.Empty;
            var space = name.IndexOf(' ');
            return space > 0 ? name[..space] : name;
        }
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ThriveWellness.Authorization
{
    // The authorization policies and role names the whole app uses, in one
    // place so Program.cs and the tests configure them identically.
    //
    // Being signed in is never enough on its own: once clients can sign in
    // too, a bare [Authorize] would let any client into the admin pages.
    // Every protected controller or action names one of these policies
    // instead (a reflection test enforces it).
    public static class AuthPolicies
    {
        public const string AdminOnly = "AdminOnly";

        public const string AdminRole = "Admin";

        public static void Configure(AuthorizationOptions options)
        {
            options.AddPolicy(AdminOnly, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimTypes.Role, AdminRole));
        }
    }
}

using System.Security.Claims;
using ThriveWellness.Authorization;
using ThriveWellness.Models;

namespace ThriveWellness.Services
{
    // Builds the public site header for whoever is looking at it:
    // - a visitor: Home, Schedule, About and a Sign in button;
    // - a client: Home, Schedule and Dashboard, then a profile menu with
    //   My rewards, About and Sign out;
    // - the admin: Home and Schedule, then a profile menu with Admin
    //   dashboard, About and Sign out. The client-only Dashboard and My
    //   rewards links are left out, since the admin would only be refused.
    // The admin's own pages keep their separate sidebar layout.
    public static class HeaderNav
    {
        public static HeaderNavViewModel Build(ClaimsPrincipal user, string controller, string action)
        {
            bool On(string c, string a) =>
                string.Equals(controller, c, StringComparison.OrdinalIgnoreCase)
                && string.Equals(action, a, StringComparison.OrdinalIgnoreCase);

            var home = new NavLink("Home", "Home", "Index", On("Home", "Index"));
            // Every booking page (schedule, booking form, cancellation)
            // counts as Schedule, as before.
            var schedule = new NavLink("Schedule", "Booking", "Schedule",
                string.Equals(controller, "Booking", StringComparison.OrdinalIgnoreCase));
            var about = new NavLink("About", "Home", "About", On("Home", "About"));

            var isAdmin = user.Identity?.IsAuthenticated == true && user.IsInRole(AuthPolicies.AdminRole);

            if (user.IsClient())
            {
                return new HeaderNavViewModel
                {
                    MainLinks = new[] { home, schedule, new NavLink("Dashboard", "Account", "Dashboard", On("Account", "Dashboard")) },
                    Account = AccountFor(user, isAdmin: false, new[]
                    {
                        new NavLink("My rewards", "Account", "Dashboard", Fragment: "rewards"),
                        about
                    })
                };
            }

            if (isAdmin)
            {
                return new HeaderNavViewModel
                {
                    MainLinks = new[] { home, schedule },
                    Account = AccountFor(user, isAdmin: true, new[]
                    {
                        new NavLink("Admin dashboard", "Admin", "Index"),
                        about
                    })
                };
            }

            return new HeaderNavViewModel
            {
                MainLinks = new[] { home, schedule, about },
                SignIn = new NavLink("Sign in", "Account", "ClientLogin", On("Account", "ClientLogin"))
            };
        }

        private static HeaderAccount AccountFor(ClaimsPrincipal user, bool isAdmin, IReadOnlyList<NavLink> items)
        {
            var name = user.Identity?.Name?.Trim() ?? string.Empty;
            var firstName = FirstNameOf(name);
            return new HeaderAccount
            {
                Name = name,
                FirstName = firstName,
                Initial = firstName.Length > 0 ? char.ToUpperInvariant(firstName[0]).ToString() : "?",
                Subtitle = isAdmin ? "Studio admin" : user.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                IsAdmin = isAdmin,
                Items = items,
                SignOutAction = isAdmin ? "Logout" : "ClientSignOut"
            };
        }

        // "Jane" from "Jane Doe" - the same rule as ClientClaims.FirstName.
        private static string FirstNameOf(string name)
        {
            var space = name.IndexOf(' ');
            return space > 0 ? name[..space] : name;
        }
    }
}

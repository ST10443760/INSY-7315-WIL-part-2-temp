namespace ThriveWellness.Models
{
    // What the public site header shows for the current visitor - see
    // HeaderNav.Build. The view only renders this; who sees which link is
    // decided in one tested place.
    public class HeaderNavViewModel
    {
        // Always shown in the bar (and at the top of the mobile menu).
        public IReadOnlyList<NavLink> MainLinks { get; set; } = Array.Empty<NavLink>();

        // The Sign in button, for visitors who aren't signed in.
        public NavLink? SignIn { get; set; }

        // The profile menu, for a signed-in client or the admin.
        public HeaderAccount? Account { get; set; }
    }

    public record NavLink(string Label, string Controller, string Action, bool IsActive = false, string? Fragment = null);

    public class HeaderAccount
    {
        public string Name { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;

        // The letter on the round profile button.
        public string Initial { get; set; } = string.Empty;

        // The client's email, or a short role line for the admin.
        public string Subtitle { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }

        // The menu's links, in order, before Sign out.
        public IReadOnlyList<NavLink> Items { get; set; } = Array.Empty<NavLink>();

        // Clients and the admin sign out through different actions (they
        // end the same cookie, but each returns to its own page).
        public string SignOutAction { get; set; } = string.Empty;
    }
}

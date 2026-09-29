namespace ThriveWellness.Models
{
    // Session.SessionType is stored lowercase ("group"/"private") for
    // matching/filtering; this maps it to the friendly label used on the
    // public site (matches the homepage's own "Group Classes"/"Private
    // Sessions" feature-card copy).
    public static class SessionTypeDisplay
    {
        public static string ToDisplayLabel(this string sessionType)
        {
            return sessionType?.ToLowerInvariant() switch
            {
                "group" => "Group Class",
                "private" => "Private Session",
                _ => sessionType ?? string.Empty
            };
        }
    }
}

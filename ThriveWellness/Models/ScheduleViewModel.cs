namespace ThriveWellness.Models
{
    // View model for the public schedule page - the session list plus
    // enough state (the location dropdown's options, what's currently
    // selected) to redraw the filter controls after a filtered request.
    public class ScheduleViewModel
    {
        public IEnumerable<SessionListItemViewModel> Sessions { get; set; } = Enumerable.Empty<SessionListItemViewModel>();
        public IEnumerable<Location> Locations { get; set; } = Enumerable.Empty<Location>();

        // Bare Location.Address value (e.g. "Sunninghill"), or null for "All Locations".
        public string? SelectedLocation { get; set; }
        public DateTime? SelectedDate { get; set; }
    }
}

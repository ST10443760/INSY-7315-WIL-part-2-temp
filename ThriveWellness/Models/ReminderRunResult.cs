namespace ThriveWellness.Models
{
    // Return value of ScheduledNotificationService.SendDueRemindersAsync -
    // what one run of the day-before reminder sweep actually did. Shown to
    // the admin after "Send reminders now"; the hourly background run just
    // ignores it.
    public class ReminderRunResult
    {
        // Reminder emails that went out on this run.
        public int Sent { get; set; }

        // Paid bookings for tomorrow that already had a reminder logged, so
        // were deliberately not emailed again.
        public int Skipped { get; set; }

        // Reminders that were due but threw while sending - logged, and
        // still due, so the next run tries them again.
        public int Failed { get; set; }

        // The short banner text the admin sees, e.g. "Sent 3 reminders,
        // skipped 1". Failures are only mentioned when there were any.
        public string ToSummary()
        {
            var summary = $"Sent {Sent} {(Sent == 1 ? "reminder" : "reminders")}, skipped {Skipped}";
            if (Failed > 0)
            {
                summary += $", {Failed} failed";
            }
            return summary;
        }
    }
}

using ThriveWellness.Models;

namespace ThriveWellness.Services
{
    // The one definition of "attended", used by the client dashboard tile,
    // rewards (badges and streaks) and the admin Clients page so they can
    // never disagree. A booking counts as attended when it isn't cancelled,
    // its class has started, it's paid for, and the admin hasn't marked it a
    // no-show.
    //
    // "Paid" means the payment is Confirmed - by the admin for EFT and cash,
    // or automatically after a card charge. A Confirmed booking with no
    // payment row at all (older data) also counts, since a booking only ever
    // becomes Confirmed once its payment does.
    public static class Attendance
    {
        public static bool IsAttended(string bookingStatus, bool noShow, string? paymentStatus, DateTime classStartsAt, DateTime now)
        {
            return bookingStatus != "Cancelled"
                && !noShow
                && classStartsAt <= now
                && IsPaid(bookingStatus, paymentStatus);
        }

        public static bool IsPaid(string bookingStatus, string? paymentStatus) =>
            paymentStatus == PaymentStatuses.Confirmed || (paymentStatus == null && bookingStatus == "Confirmed");
    }
}

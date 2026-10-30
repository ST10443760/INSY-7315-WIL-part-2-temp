namespace ThriveWellness.Models
{
    // The values Payment.Status can hold. Pending until it's paid, then
    // Confirmed - by the admin for EFT and cash, or automatically once a
    // card charge succeeds at checkout.
    public static class PaymentStatuses
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
    }
}

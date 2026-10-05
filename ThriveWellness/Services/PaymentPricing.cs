namespace ThriveWellness.Services
{
    // Shared between BookingService (which uses this to work out what a new
    // booking's Payment.Amount should be) and NotificationService (whose
    // confirmation email describes that same amount) - one place for the
    // two pricing tiers (FR-06), so neither R120 nor R450 is ever written
    // down as a separate literal a second time.
    public static class PaymentPricing
    {
        public const decimal PerClassPrice = 120m;
        public const decimal MonthlyPrice = 450m;

        public static decimal GetAmountForPaymentType(string paymentType)
        {
            return paymentType switch
            {
                "monthly" => MonthlyPrice,
                "per-class" => PerClassPrice,
                _ => throw new ArgumentException($"Unknown payment type: {paymentType}")
            };
        }
    }
}

namespace ThriveWellness.Models
{
    // The card brands the checkout recognises, as stored on
    // PaymentTransaction.Brand and shown on receipts.
    public static class CardBrands
    {
        public const string Visa = "Visa";
        public const string Mastercard = "Mastercard";
        public const string Amex = "American Express";
        public const string Other = "Card";
    }
}

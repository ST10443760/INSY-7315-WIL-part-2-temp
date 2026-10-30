namespace ThriveWellness.Models
{
    // The values Payment.Method can hold. EFT and cash keep the exact
    // spellings already stored on existing rows ("EFT", "cash"); Card is
    // paid online through IPaymentGateway at checkout.
    public static class PaymentMethods
    {
        public const string Card = "Card";
        public const string Eft = "EFT";
        public const string Cash = "cash";

        public static readonly IReadOnlyList<string> All = new[] { Card, Eft, Cash };

        public static bool IsCard(string? method) =>
            string.Equals(method, Card, StringComparison.OrdinalIgnoreCase);
    }
}

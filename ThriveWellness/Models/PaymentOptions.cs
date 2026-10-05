namespace ThriveWellness.Models
{
    // Bound from the "Payment" configuration section (Program.cs) via the
    // Options pattern - on Render that's the Payment__AccountHolder,
    // Payment__Bank and Payment__AccountNumber environment variables,
    // mapped the same way Admin__Username/Admin__Password already are.
    // Any of these can be missing (appsettings.json ships only empty
    // placeholders); NotificationService is responsible for falling back
    // safely rather than emailing a client blanks or stale placeholder text.
    public class PaymentOptions
    {
        public string? AccountHolder { get; set; }
        public string? Bank { get; set; }
        public string? AccountNumber { get; set; }
    }
}

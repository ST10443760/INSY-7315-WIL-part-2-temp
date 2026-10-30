using System.ComponentModel.DataAnnotations;

namespace ThriveWellness.Models
{
    // The card form as posted from the checkout page. Not an entity and
    // never stored: the controller turns it into CardDetails for the
    // gateway, and only the cardholder name is ever shown again after a
    // failed attempt - the number, expiry and CVC are always re-typed.
    public class CardPaymentForm
    {
        [Required(ErrorMessage = "Enter the name on your card.")]
        [StringLength(100)]
        [Display(Name = "Name on card")]
        public string CardholderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your card number.")]
        [Display(Name = "Card number")]
        public string CardNumber { get; set; } = string.Empty;

        // As typed, "MM/YY".
        [Required(ErrorMessage = "Enter your card's expiry date.")]
        [Display(Name = "Expiry")]
        public string Expiry { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your card's security code.")]
        [Display(Name = "CVC")]
        public string Cvc { get; set; } = string.Empty;
    }
}

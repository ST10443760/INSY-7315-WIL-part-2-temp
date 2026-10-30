using System.Security.Cryptography;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Stands in for a real card provider (PayFast, Stripe) behind the same
    // IPaymentGateway interface: no bank is contacted and no money moves.
    // It checks the card the way a provider would (Luhn, expiry, CVC),
    // answers the published test cards with their usual outcomes, and hands
    // back a gateway reference, brand and last four digits just like the
    // real thing. Nothing about the card is stored or logged here.
    public class SimulatedCardGateway : IPaymentGateway
    {
        public const string DeclinedCard = "4000000000000002";
        public const string InsufficientFundsCard = "4000000000009995";

        private const string ReferenceAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _processingDelay;

        // About as long as a real provider takes, so the checkout's
        // processing state is actually seen.
        public SimulatedCardGateway(TimeProvider timeProvider)
            : this(timeProvider, TimeSpan.FromSeconds(1.5))
        {
        }

        // Tests pass TimeSpan.Zero so they don't wait on the delay.
        public SimulatedCardGateway(TimeProvider timeProvider, TimeSpan processingDelay)
        {
            _timeProvider = timeProvider;
            _processingDelay = processingDelay;
        }

        public async Task<GatewayChargeResult> ChargeAsync(GatewayChargeRequest request, CancellationToken cancellationToken = default)
        {
            if (_processingDelay > TimeSpan.Zero)
            {
                await Task.Delay(_processingDelay, cancellationToken);
            }

            var number = request.Card.Number ?? string.Empty;
            var brand = DetectBrand(number);
            var last4 = number.Length >= 4 && number.All(char.IsDigit) ? number[^4..] : string.Empty;

            if (!IsValidNumber(number, brand))
            {
                return Failure(brand, last4, "invalid_number", "Your card number is incorrect.", charged: false);
            }

            if (!IsValidExpiry(request.Card.ExpiryMonth, request.Card.ExpiryYear))
            {
                return Failure(brand, last4, "expired_card", "Your card has expired.", charged: false);
            }

            var cvc = request.Card.Cvc ?? string.Empty;
            var cvcLength = brand == CardBrands.Amex ? 4 : 3;
            if (cvc.Length != cvcLength || !cvc.All(char.IsDigit))
            {
                return Failure(brand, last4, "invalid_cvc", "Your card's security code is incorrect.", charged: false);
            }

            if (number == DeclinedCard)
            {
                return Failure(brand, last4, "card_declined", "Your card was declined.", charged: true);
            }

            if (number == InsufficientFundsCard)
            {
                return Failure(brand, last4, "insufficient_funds", "Insufficient funds.", charged: true);
            }

            return new GatewayChargeResult
            {
                Success = true,
                GatewayReference = NewReference(),
                Brand = brand,
                Last4 = last4
            };
        }

        // Visa starts with 4; Mastercard with 51-55 or 2221-2720; American
        // Express with 34 or 37. Anything else is just "Card".
        public static string DetectBrand(string number)
        {
            if (string.IsNullOrEmpty(number) || !number.All(char.IsDigit))
            {
                return CardBrands.Other;
            }

            if (number.StartsWith('4'))
            {
                return CardBrands.Visa;
            }

            if (number.StartsWith("34") || number.StartsWith("37"))
            {
                return CardBrands.Amex;
            }

            if (number.Length >= 2 && int.Parse(number[..2]) is >= 51 and <= 55)
            {
                return CardBrands.Mastercard;
            }

            if (number.Length >= 4 && int.Parse(number[..4]) is >= 2221 and <= 2720)
            {
                return CardBrands.Mastercard;
            }

            return CardBrands.Other;
        }

        // The standard Luhn (mod 10) checksum every card number carries.
        public static bool PassesLuhn(string number)
        {
            if (string.IsNullOrEmpty(number) || !number.All(char.IsDigit))
            {
                return false;
            }

            var sum = 0;
            var doubleIt = false;
            for (var i = number.Length - 1; i >= 0; i--)
            {
                var digit = number[i] - '0';
                if (doubleIt)
                {
                    digit *= 2;
                    if (digit > 9)
                    {
                        digit -= 9;
                    }
                }
                sum += digit;
                doubleIt = !doubleIt;
            }

            return sum % 10 == 0;
        }

        private static bool IsValidNumber(string number, string brand)
        {
            if (!PassesLuhn(number))
            {
                return false;
            }

            return brand switch
            {
                CardBrands.Amex => number.Length == 15,
                CardBrands.Mastercard => number.Length == 16,
                CardBrands.Visa => number.Length is 13 or 16 or 19,
                _ => number.Length is >= 12 and <= 19
            };
        }

        // A card is good until the end of its expiry month.
        private bool IsValidExpiry(int month, int year)
        {
            if (month is < 1 or > 12 || year < 2000)
            {
                return false;
            }

            var today = _timeProvider.GetLocalNow().Date;
            var firstDayAfterExpiry = new DateTime(year, month, 1).AddMonths(1);
            return today < firstDayAfterExpiry;
        }

        private static GatewayChargeResult Failure(string brand, string last4, string code, string message, bool charged)
        {
            return new GatewayChargeResult
            {
                Success = false,
                // A provider only issues a reference once it has actually
                // sent the charge to the bank; a card it rejected up front
                // never got that far.
                GatewayReference = charged ? NewReference() : string.Empty,
                Brand = brand,
                Last4 = last4,
                FailureCode = code,
                FailureMessage = message
            };
        }

        private static string NewReference() =>
            "TW-" + RandomNumberGenerator.GetString(ReferenceAlphabet, 10);
    }
}

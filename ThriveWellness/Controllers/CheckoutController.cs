using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Authorization;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: the online card checkout for a booking and its receipt.
// Signed-in clients only, and only for their own bookings - CheckoutService
// makes those decisions; this controller just turns them into pages. The
// card fields are read from the form, handed to the service and dropped:
// they're never logged, stored or written back into a page.
[Authorize(Policy = AuthPolicies.ClientOnly)]
public class CheckoutController : Controller
{
    private static readonly Regex ExpiryPattern = new(@"^\s*(\d{1,2})\s*/\s*(\d{2}|\d{4})\s*$");

    // The form fields that must never be echoed back after a failed attempt.
    private static readonly string[] SensitiveFields =
    {
        nameof(CardPaymentForm.CardNumber), nameof(CardPaymentForm.Expiry), nameof(CardPaymentForm.Cvc)
    };

    private readonly ICheckoutService _checkoutService;
    private readonly IPaymentGateway _gateway;

    public CheckoutController(ICheckoutService checkoutService, IPaymentGateway gateway)
    {
        _checkoutService = checkoutService;
        _gateway = gateway;
    }

    [HttpGet("Checkout/Pay/{bookingId:int}")]
    public async Task<IActionResult> Pay(int bookingId)
    {
        var result = await _checkoutService.GetCheckoutAsync(bookingId, User.GetClientId()!.Value);
        return CheckoutResponse(result, cardholderName: User.Identity?.Name ?? string.Empty);
    }

    [HttpPost("Checkout/Pay/{bookingId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int bookingId, CardPaymentForm form, CancellationToken cancellationToken)
    {
        var clientId = User.GetClientId()!.Value;

        if (!ModelState.IsValid || !TryReadCard(form, out var card))
        {
            var current = await _checkoutService.GetCheckoutAsync(bookingId, clientId);
            if (current.Status == CheckoutStatus.Ready)
            {
                current.Message = ModelState.IsValid
                    ? "Enter the expiry date as MM/YY."
                    : ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
                current.Status = CheckoutStatus.Declined;
            }
            return CheckoutResponse(current, form.CardholderName);
        }

        var result = await _checkoutService.PayAsync(bookingId, clientId, card, cancellationToken);
        return CheckoutResponse(result, form.CardholderName);
    }

    [HttpGet("Checkout/Receipt/{paymentId:int}")]
    public async Task<IActionResult> Receipt(int paymentId)
    {
        var result = await _checkoutService.GetReceiptAsync(paymentId, User.GetClientId()!.Value);
        return result.Status switch
        {
            ReceiptStatus.Ready => View(result.Receipt),
            ReceiptStatus.NotOwner => Forbid(),
            _ => NotFound()
        };
    }

    private IActionResult CheckoutResponse(CheckoutResult result, string cardholderName)
    {
        switch (result.Status)
        {
            case CheckoutStatus.NotFound:
                return NotFound();
            case CheckoutStatus.NotOwner:
                return Forbid();
            case CheckoutStatus.AlreadyPaid:
            case CheckoutStatus.Succeeded:
                return RedirectToAction(nameof(Receipt), new { paymentId = result.PaymentId });
        }

        // Whatever was typed for the card is dropped here, so the page can
        // only ever show empty number, expiry and CVC fields.
        foreach (var field in SensitiveFields)
        {
            ModelState.Remove(field);
        }

        var model = new CheckoutViewModel
        {
            Summary = result.Summary!,
            CardholderName = cardholderName,
            ErrorMessage = result.Status is CheckoutStatus.Declined or CheckoutStatus.InProgress ? result.Message : null,
            UnavailableMessage = result.Status == CheckoutStatus.NotPayable ? result.Message : null,
            IsTestMode = _gateway.IsTestMode
        };

        return View("Pay", model);
    }

    // Digits only for the number and CVC (the form adds spaces as the client
    // types), and MM/YY expanded to a month and four-digit year.
    private static bool TryReadCard(CardPaymentForm form, out CardDetails card)
    {
        card = new CardDetails
        {
            CardholderName = form.CardholderName.Trim(),
            Number = new string(form.CardNumber.Where(char.IsDigit).ToArray()),
            Cvc = form.Cvc.Trim()
        };

        var match = ExpiryPattern.Match(form.Expiry);
        if (!match.Success)
        {
            return false;
        }

        var year = int.Parse(match.Groups[2].Value);
        card.ExpiryMonth = int.Parse(match.Groups[1].Value);
        card.ExpiryYear = year < 100 ? 2000 + year : year;
        return true;
    }
}

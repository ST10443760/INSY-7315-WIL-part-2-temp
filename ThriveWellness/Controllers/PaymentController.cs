using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Authorization;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: the admin payments list and the "mark as confirmed"
// action. The AdminOnly policy means every action here requires the admin
// sign-in from AccountController.Login (a signed-in client is refused) -
// there's no payment-specific permission beyond being the admin.
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class PaymentController : Controller
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentService _paymentService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRewardsService _rewardsService;

    public PaymentController(
        IPaymentRepository paymentRepository,
        IPaymentService paymentService,
        IBookingRepository bookingRepository,
        IRewardsService rewardsService)
    {
        _paymentRepository = paymentRepository;
        _paymentService = paymentService;
        _bookingRepository = bookingRepository;
        _rewardsService = rewardsService;
    }

    // status: null/"all" for everything, or "Pending"/"Confirmed" to filter -
    // matches the All/Pending/Confirmed tabs on the page itself.
    [HttpGet]
    public async Task<IActionResult> Index(string? status)
    {
        var filterStatus = string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? null
            : status;

        var payments = await _paymentRepository.GetAllPaymentsAsync(filterStatus);
        ViewData["SelectedStatus"] = filterStatus ?? "all";
        return View(payments);
    }

    [HttpPost("Payment/Confirm/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, string? status)
    {
        var emailSent = await _paymentService.ConfirmPaymentAsync(id);

        // Paying for a class that has already happened can complete it as
        // attended, so check the client's badges.
        await EvaluateRewardsForPaymentAsync(id);

        TempData["PaymentBannerMessage"] = "Payment confirmed.";
        if (!emailSent)
        {
            TempData["PaymentBannerWarning"] = "Payment confirmed, but the email could not be sent.";
        }

        // Sends the admin back to whichever tab they confirmed this from,
        // instead of always resetting to "All".
        return RedirectToAction(nameof(Index), new { status });
    }

    // The studio has paid an EFT or cash refund back: Refund due becomes
    // Refunded, so it drops off the Refund due tab.
    [HttpPost("Payment/MarkRefunded/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRefunded(int id, string? status)
    {
        TempData["PaymentBannerMessage"] = await _paymentService.MarkRefundedAsync(id)
            ? "Payment marked as refunded."
            : "That payment isn't waiting for a refund.";
        return RedirectToAction(nameof(Index), new { status });
    }

    private async Task EvaluateRewardsForPaymentAsync(int paymentId)
    {
        var payment = await _paymentRepository.GetByIdAsync(paymentId);
        var booking = payment != null ? await _bookingRepository.GetByIdAsync(payment.BookingId) : null;
        if (booking != null)
        {
            await _rewardsService.EvaluateAsync(booking.ClientId);
        }
    }
}

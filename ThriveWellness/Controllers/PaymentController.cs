using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: the admin payments list and the "mark as confirmed"
// action. [Authorize] means every action here requires the admin sign-in
// cookie from AccountController.Login - there's no payment-specific
// permission, just "signed in or not".
[Authorize]
public class PaymentController : Controller
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentRepository paymentRepository, IPaymentService paymentService)
    {
        _paymentRepository = paymentRepository;
        _paymentService = paymentService;
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
        await _paymentService.ConfirmPaymentAsync(id);
        // Sends the admin back to whichever tab they confirmed this from,
        // instead of always resetting to "All".
        return RedirectToAction(nameof(Index), new { status });
    }
}

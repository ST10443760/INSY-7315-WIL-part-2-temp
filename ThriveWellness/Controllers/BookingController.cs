using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Authorization;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: the public schedule page and the entire client-facing
// booking flow - the booking details form, joining the waitlist directly
// from a full session, the booking received page, and both cancellation
// paths (client link, FR-18; admin stub, FR-12). Booking and the waitlist
// need a signed-in client (ClientOnly); the schedule and the emailed
// cancellation link stay open to everyone. Pulls in BookingService,
// SessionService and WaitlistService rather than talking to EF Core itself,
// keeping the actual business rules in the service layer.
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly ISessionService _sessionService;
    private readonly ILocationRepository _locationRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IWaitlistService _waitlistService;
    private readonly IPaymentRepository _paymentRepository;

    public BookingController(
        IBookingService bookingService,
        ISessionService sessionService,
        ILocationRepository locationRepository,
        IClientRepository clientRepository,
        IWaitlistService waitlistService,
        IPaymentRepository paymentRepository)
    {
        _bookingService = bookingService;
        _sessionService = sessionService;
        _locationRepository = locationRepository;
        _clientRepository = clientRepository;
        _waitlistService = waitlistService;
        _paymentRepository = paymentRepository;
    }

    // Lists every session - open, full and closed - so a full class still
    // shows up with a "Join Waitlist" card instead of just disappearing.
    // location/date are optional server-side filters from the schedule's
    // tabs and date picker. week (?week=YYYY-MM-DD) picks the Monday-to-
    // Sunday week to show - see ScheduleWeek for the snapping and clamping
    // rules. A picked date takes the page to that date's week.
    public async Task<IActionResult> Schedule(string? location, DateTime? date, DateTime? week)
    {
        var lastSessionDate = await _sessionService.GetLastUpcomingSessionDateAsync();
        var scheduleWeek = ScheduleWeek.Resolve(date ?? week, DateTime.Today, lastSessionDate);

        var sessions = await _sessionService.GetScheduleAsync(location, date, scheduleWeek.Start);
        var locations = await _locationRepository.GetAllAsync();

        var viewModel = new ScheduleViewModel
        {
            Sessions = sessions,
            Locations = locations,
            SelectedLocation = location,
            SelectedDate = date,
            Week = scheduleWeek
        };

        return View(viewModel);
    }

    // Booking needs a signed-in client: anyone else is sent to the client
    // sign-in page and brought straight back here afterwards (see
    // OnRedirectToLogin in Program.cs). There's no email step any more - the
    // details form is pre-filled from the client's own account.
    [HttpGet("Booking/Book/{sessionId:int}")]
    [Authorize(Policy = AuthPolicies.ClientOnly)]
    public async Task<IActionResult> Book(int sessionId)
    {
        var session = await _sessionService.GetByIdAsync(sessionId);
        if (session == null)
        {
            return NotFound();
        }

        // Still blocked server-side: a full/closed session can't be booked
        // even if this URL is reached directly (bookmark, back button,
        // someone else grabbing the last spot in the meantime).
        var bookedCount = await _sessionService.GetBookedCountAsync(sessionId);
        if (!session.IsOpen || bookedCount >= session.Capacity)
        {
            return RedirectToAction(nameof(JoinWaitlist), new { sessionId });
        }

        var client = await _clientRepository.GetByIdAsync(User.GetClientId()!.Value);
        if (client == null)
        {
            return await ClientAccountMissingAsync();
        }

        var model = new BookingSubmitViewModel
        {
            SessionId = session.SessionId,
            FullName = client.FullName,
            PhoneNumber = client.PhoneNumber,
            Method = PaymentMethods.Card
        };
        await FillDetailsDisplayAsync(model, session, client);

        return View("Details", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthPolicies.ClientOnly)]
    // Booking flow's final step: a few cross-field rules that don't fit a
    // single data annotation (consent only required on a first booking;
    // payment type/method always required) on top of the model's own
    // validation, then hands off to BookingService.CreateBookingAsync for
    // the signed-in client. Branches three ways on the result: the full
    // session's waitlist (RequiresWaitlist), a validation-style failure
    // shown back on the same form, or success redirecting to the booking
    // received page.
    public async Task<IActionResult> Submit(BookingSubmitViewModel model)
    {
        var session = await _sessionService.GetByIdAsync(model.SessionId);
        if (session == null)
        {
            return NotFound();
        }
        var location = await _locationRepository.GetByIdAsync(session.LocationId);

        var client = await _clientRepository.GetByIdAsync(User.GetClientId()!.Value);
        if (client == null)
        {
            return await ClientAccountMissingAsync();
        }

        // Worked out again here rather than trusted from the form, so the
        // consent rule can't be skipped by editing a hidden field.
        var isFirstBooking = await _bookingService.IsFirstBookingAsync(client.ClientId);

        if (isFirstBooking && !model.ConsentSigned)
        {
            ModelState.AddModelError(nameof(model.ConsentSigned), "Consent is required to complete your booking.");
        }

        if (string.IsNullOrWhiteSpace(model.PaymentType))
        {
            ModelState.AddModelError(nameof(model.PaymentType), "Payment type is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Method))
        {
            ModelState.AddModelError(nameof(model.Method), "Payment method is required.");
        }
        else if (!PaymentMethods.All.Contains(model.Method))
        {
            ModelState.AddModelError(nameof(model.Method), "Please choose card, EFT or cash.");
        }

        if (!ModelState.IsValid)
        {
            await FillDetailsDisplayAsync(model, session, client);
            return View("Details", model);
        }

        var request = new BookingRequest
        {
            ClientId = client.ClientId,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            PaymentType = model.PaymentType,
            Method = model.Method,
            SessionId = model.SessionId,
            MedicalNotes = model.MedicalNotes,
            ConsentSigned = model.ConsentSigned
        };

        var result = await _bookingService.CreateBookingAsync(request);

        if (result.RequiresWaitlist)
        {
            var waitlistEntry = await _waitlistService.JoinWaitlistAsync(result.ClientId!.Value, model.SessionId);

            var waitlistViewModel = new WaitlistJoinedViewModel
            {
                SessionType = session.SessionType,
                SessionDate = session.Date,
                SessionTime = session.Time,
                LocationName = location?.Name ?? "Unknown",
                LocationAddress = location?.Address ?? string.Empty,
                Position = waitlistEntry.Position
            };
            return View("WaitlistJoined", waitlistViewModel);
        }

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to complete the booking.");
            await FillDetailsDisplayAsync(model, session, client);
            return View("Details", model);
        }

        // A card booking is saved exactly like an EFT or cash one (awaiting
        // payment, booking received email sent), then goes straight on to
        // pay. A client who leaves checkout can come back from the booking
        // received page or their dashboard.
        if (PaymentMethods.IsCard(model.Method))
        {
            return RedirectToAction("Pay", "Checkout", new { bookingId = result.BookingId });
        }

        return RedirectToAction(nameof(Confirmation), new { bookingId = result.BookingId });
    }

    [HttpGet("Booking/Confirmation/{bookingId:int}")]
    [Authorize(Policy = AuthPolicies.ClientOnly)]
    // The booking received page, reached right after Submit succeeds -
    // assembles the flattened view model from four separate lookups
    // (booking, client, session+location, payment) since nothing else needs
    // that join and it isn't worth a dedicated repository method for it.
    // Only the client who made the booking can open it; anyone else gets a
    // plain 404 rather than someone else's booking details.
    public async Task<IActionResult> Confirmation(int bookingId)
    {
        var booking = await _bookingService.GetByIdAsync(bookingId);
        if (booking == null || booking.ClientId != User.GetClientId())
        {
            return NotFound();
        }

        var client = await _clientRepository.GetByIdAsync(booking.ClientId);
        var session = await _sessionService.GetByIdAsync(booking.SessionId);
        var location = session != null ? await _locationRepository.GetByIdAsync(session.LocationId) : null;
        var payment = await _paymentRepository.GetByBookingIdAsync(booking.BookingId);

        var viewModel = new BookingConfirmationViewModel
        {
            BookingId = booking.BookingId,
            ClientName = client?.FullName ?? string.Empty,
            SessionType = session?.SessionType ?? string.Empty,
            SessionDate = session?.Date ?? default,
            SessionTime = session?.Time ?? default,
            LocationName = location?.Name ?? "Unknown",
            LocationAddress = location?.Address ?? string.Empty,
            Status = booking.Status,
            Method = payment?.Method ?? string.Empty,
            AmountDue = payment?.Amount,
            PaymentId = payment?.PaymentId,
            PaymentStatus = payment?.Status,
            CancellationToken = booking.CancellationToken
        };

        return View(viewModel);
    }

    // The direct "join the waitlist without booking" entry point (FR-13-ish),
    // reached from a "Fully Booked" schedule card. Booking.Book redirects
    // here too if it's reached for a session that's since filled up. Needs a
    // signed-in client, like booking.
    [HttpGet("Booking/JoinWaitlist/{sessionId:int}")]
    [Authorize(Policy = AuthPolicies.ClientOnly)]
    public async Task<IActionResult> JoinWaitlist(int sessionId)
    {
        var session = await _sessionService.GetByIdAsync(sessionId);
        if (session == null)
        {
            return NotFound();
        }

        var bookedCount = await _sessionService.GetBookedCountAsync(sessionId);
        var isFull = !session.IsOpen || bookedCount >= session.Capacity;
        if (!isFull)
        {
            // Not actually full - send them through the normal booking flow.
            return RedirectToAction(nameof(Book), new { sessionId });
        }

        var client = await _clientRepository.GetByIdAsync(User.GetClientId()!.Value);
        if (client == null)
        {
            return await ClientAccountMissingAsync();
        }

        var location = await _locationRepository.GetByIdAsync(session.LocationId);

        var viewModel = new WaitlistJoinViewModel
        {
            SessionId = session.SessionId,
            FullName = client.FullName,
            Email = client.Email,
            SessionType = session.SessionType,
            SessionDate = session.Date,
            SessionTime = session.Time,
            LocationName = location?.Name ?? "Unknown",
            LocationAddress = location?.Address ?? string.Empty
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthPolicies.ClientOnly)]
    // Completes the direct waitlist join for the signed-in client - the
    // entry attaches to their own client id, never to anything typed into
    // the form.
    public async Task<IActionResult> JoinWaitlistSubmit(int sessionId)
    {
        var session = await _sessionService.GetByIdAsync(sessionId);
        if (session == null)
        {
            return NotFound();
        }
        var location = await _locationRepository.GetByIdAsync(session.LocationId);

        var client = await _clientRepository.GetByIdAsync(User.GetClientId()!.Value);
        if (client == null)
        {
            return await ClientAccountMissingAsync();
        }

        var entry = await _waitlistService.JoinWaitlistAsync(client.ClientId, sessionId);

        var waitlistViewModel = new WaitlistJoinedViewModel
        {
            SessionType = session.SessionType,
            SessionDate = session.Date,
            SessionTime = session.Time,
            LocationName = location?.Name ?? "Unknown",
            LocationAddress = location?.Address ?? string.Empty,
            Position = entry.Position
        };

        return View("WaitlistJoined", waitlistViewModel);
    }

    // Repopulates the details form's display-only fields (session summary,
    // the account email, whether the intake fields show) on every render.
    private async Task FillDetailsDisplayAsync(BookingSubmitViewModel model, Session session, Client client)
    {
        var location = await _locationRepository.GetByIdAsync(session.LocationId);
        model.Email = client.Email;
        model.IsNewClient = await _bookingService.IsFirstBookingAsync(client.ClientId);
        model.SessionType = session.SessionType;
        model.SessionDate = session.Date;
        model.SessionTime = session.Time;
        model.LocationName = location?.Name ?? "Unknown";
    }

    // A valid client cookie whose client row no longer exists (for example
    // after the demo data was reset): sign out and sign in again rather
    // than error. Signing out first matters - the sign-in page sends an
    // already signed-in client straight back here.
    private async Task<IActionResult> ClientAccountMissingAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("ClientLogin", "Account", new { returnUrl = Request.Path + Request.QueryString });
    }

    [HttpGet("Booking/Cancel/{token}")]
    // The FR-18 cancellation link's destination - no [Authorize], since this
    // is the one booking action a client (who never logs in) performs
    // themselves, authenticated only by possession of the token itself.
    public async Task<IActionResult> Cancel(string token)
    {
        var result = await _bookingService.CancelBookingAsync(token);
        return View("CancelResult", result);
    }

    // Minimal admin-initiated cancel/reschedule stub (FR-12): just enough to
    // trigger SendCancellationEmailAsync. There's no admin bookings list to
    // link this from yet - that's a separate feature - so for now this is
    // reached directly with a known booking id.
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminCancel(int bookingId)
    {
        var result = await _bookingService.CancelByAdminAsync(bookingId);
        return View("CancelResult", result);
    }
}

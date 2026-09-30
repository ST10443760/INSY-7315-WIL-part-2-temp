using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

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
    // tabs and date picker.
    public async Task<IActionResult> Schedule(string? location, DateTime? date)
    {
        var sessions = await _sessionService.GetScheduleAsync(location, date);
        var locations = await _locationRepository.GetAllAsync();

        var viewModel = new ScheduleViewModel
        {
            Sessions = sessions,
            Locations = locations,
            SelectedLocation = location,
            SelectedDate = date
        };

        return View(viewModel);
    }

    [HttpGet("Booking/Book/{sessionId:int}")]
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

        var location = await _locationRepository.GetByIdAsync(session.LocationId);

        var viewModel = new BookingEmailStepViewModel
        {
            SessionId = session.SessionId,
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
    public async Task<IActionResult> CheckEmail(BookingEmailStepViewModel model)
    {
        var session = await _sessionService.GetByIdAsync(model.SessionId);
        if (session == null)
        {
            return NotFound();
        }
        var location = await _locationRepository.GetByIdAsync(session.LocationId);

        if (!ModelState.IsValid)
        {
            model.SessionType = session.SessionType;
            model.SessionDate = session.Date;
            model.SessionTime = session.Time;
            model.LocationName = location?.Name ?? "Unknown";
            model.LocationAddress = location?.Address ?? string.Empty;
            return View("Book", model);
        }

        var status = await _bookingService.CheckClientStatusAsync(model.Email);

        var detailsModel = new BookingSubmitViewModel
        {
            SessionId = session.SessionId,
            Email = model.Email,
            IsNewClient = status.IsNew,
            FullName = status.FullName ?? string.Empty,
            PhoneNumber = status.PhoneNumber ?? string.Empty,
            SessionType = session.SessionType,
            SessionDate = session.Date,
            SessionTime = session.Time,
            LocationName = location?.Name ?? "Unknown"
        };

        return View("Details", detailsModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(BookingSubmitViewModel model)
    {
        var session = await _sessionService.GetByIdAsync(model.SessionId);
        if (session == null)
        {
            return NotFound();
        }
        var location = await _locationRepository.GetByIdAsync(session.LocationId);

        if (model.IsNewClient && !model.ConsentSigned)
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

        if (!ModelState.IsValid)
        {
            model.SessionType = session.SessionType;
            model.SessionDate = session.Date;
            model.SessionTime = session.Time;
            model.LocationName = location?.Name ?? "Unknown";
            return View("Details", model);
        }

        var request = new BookingRequest
        {
            Email = model.Email,
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

            model.SessionType = session.SessionType;
            model.SessionDate = session.Date;
            model.SessionTime = session.Time;
            model.LocationName = location?.Name ?? "Unknown";
            return View("Details", model);
        }

        return RedirectToAction(nameof(Confirmation), new { bookingId = result.BookingId });
    }

    [HttpGet("Booking/Confirmation/{bookingId:int}")]
    public async Task<IActionResult> Confirmation(int bookingId)
    {
        var booking = await _bookingService.GetByIdAsync(bookingId);
        if (booking == null)
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
            CancellationToken = booking.CancellationToken
        };

        return View(viewModel);
    }

    // The direct "join the waitlist without booking" entry point (FR-13-ish),
    // reached from a "Fully Booked" schedule card. Booking.Book redirects
    // here too if it's reached for a session that's since filled up.
    [HttpGet("Booking/JoinWaitlist/{sessionId:int}")]
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

        var location = await _locationRepository.GetByIdAsync(session.LocationId);

        var viewModel = new WaitlistJoinViewModel
        {
            SessionId = session.SessionId,
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
    public async Task<IActionResult> JoinWaitlistSubmit(WaitlistJoinViewModel model)
    {
        var session = await _sessionService.GetByIdAsync(model.SessionId);
        if (session == null)
        {
            return NotFound();
        }
        var location = await _locationRepository.GetByIdAsync(session.LocationId);

        if (!ModelState.IsValid)
        {
            model.SessionType = session.SessionType;
            model.SessionDate = session.Date;
            model.SessionTime = session.Time;
            model.LocationName = location?.Name ?? "Unknown";
            model.LocationAddress = location?.Address ?? string.Empty;
            return View("JoinWaitlist", model);
        }

        var client = await _clientRepository.GetByEmailAsync(model.Email);
        if (client == null)
        {
            client = new Client
            {
                FullName = model.FullName,
                Email = model.Email,
                PhoneNumber = string.Empty,
                IsNew = true,
                CreatedAt = DateTime.UtcNow
            };
            await _clientRepository.AddAsync(client);
        }

        var entry = await _waitlistService.JoinWaitlistAsync(client.ClientId, model.SessionId);

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

    [HttpGet("Booking/Cancel/{token}")]
    public async Task<IActionResult> Cancel(string token)
    {
        var result = await _bookingService.CancelBookingAsync(token);
        return View("CancelResult", result);
    }

    // Minimal admin-initiated cancel/reschedule stub (FR-12): just enough to
    // trigger SendCancellationEmailAsync. There's no admin bookings list to
    // link this from yet - that's a separate feature - so for now this is
    // reached directly with a known booking id.
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminCancel(int bookingId)
    {
        var result = await _bookingService.CancelByAdminAsync(bookingId);
        return View("CancelResult", result);
    }
}

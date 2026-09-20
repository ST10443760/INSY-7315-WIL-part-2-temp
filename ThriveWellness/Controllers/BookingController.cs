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

    public BookingController(
        IBookingService bookingService,
        ISessionService sessionService,
        ILocationRepository locationRepository,
        IClientRepository clientRepository,
        IWaitlistService waitlistService)
    {
        _bookingService = bookingService;
        _sessionService = sessionService;
        _locationRepository = locationRepository;
        _clientRepository = clientRepository;
        _waitlistService = waitlistService;
    }

    public async Task<IActionResult> Schedule()
    {
        var sessions = await _sessionService.GetAvailableSessionsAsync();
        var locations = (await _locationRepository.GetAllAsync()).ToDictionary(l => l.LocationId, l => l.Name);

        var viewModel = sessions.Select(s => new SessionListItemViewModel
        {
            SessionId = s.SessionId,
            LocationName = locations.TryGetValue(s.LocationId, out var name) ? name : "Unknown",
            SessionType = s.SessionType,
            Date = s.Date,
            Time = s.Time,
            Capacity = s.Capacity,
            IsOpen = s.IsOpen
        }).OrderBy(s => s.Date).ThenBy(s => s.Time);

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

        var location = await _locationRepository.GetByIdAsync(session.LocationId);

        var viewModel = new BookingEmailStepViewModel
        {
            SessionId = session.SessionId,
            SessionType = session.SessionType,
            SessionDate = session.Date,
            SessionTime = session.Time,
            LocationName = location?.Name ?? "Unknown"
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

        if (model.IsNewClient && string.IsNullOrWhiteSpace(model.PaymentType))
        {
            ModelState.AddModelError(nameof(model.PaymentType), "Payment type is required.");
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

        var viewModel = new BookingConfirmationViewModel
        {
            BookingId = booking.BookingId,
            ClientName = client?.FullName ?? string.Empty,
            SessionType = session?.SessionType ?? string.Empty,
            SessionDate = session?.Date ?? default,
            SessionTime = session?.Time ?? default,
            LocationName = location?.Name ?? "Unknown",
            Status = booking.Status,
            CancellationToken = booking.CancellationToken
        };

        return View(viewModel);
    }

    [HttpGet("Booking/Cancel/{token}")]
    public async Task<IActionResult> Cancel(string token)
    {
        var result = await _bookingService.CancelBookingAsync(token);
        return View("CancelResult", result);
    }
}

using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;
using ThriveWellness.Services;

namespace ThriveWellness.Services.Implementations
{
    public class BookingService : IBookingService
    {
        private readonly IClientRepository _clientRepository;
        private readonly IBookingRepository _bookingRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly ApplicationDbContext _context;

        public BookingService(
            IClientRepository clientRepository,
            IBookingRepository bookingRepository,
            ISessionRepository sessionRepository,
            ApplicationDbContext context)
        {
            _clientRepository = clientRepository;
            _bookingRepository = bookingRepository;
            _sessionRepository = sessionRepository;
            _context = context;
        }

        public async Task<ClientStatusResult> CheckClientStatusAsync(string email)
        {
            var client = await _clientRepository.GetByEmailAsync(email);

            if (client == null)
            {
                return new ClientStatusResult { IsNew = true };
            }

            return new ClientStatusResult
            {
                IsNew = false,
                FullName = client.FullName,
                PhoneNumber = client.PhoneNumber
            };
        }

        public async Task<BookingCreateResult> CreateBookingAsync(BookingRequest request)
        {
            var client = await _clientRepository.GetByEmailAsync(request.Email);
            var isNewClient = client == null;

            if (isNewClient)
            {
                if (!request.ConsentSigned)
                {
                    return new BookingCreateResult
                    {
                        Success = false,
                        ErrorMessage = "Consent must be given to complete the intake form."
                    };
                }

                client = new Client
                {
                    FullName = request.FullName,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    IsNew = true,
                    PaymentType = request.PaymentType
                };
                await _clientRepository.AddAsync(client);
            }
            else if (client!.IsNew)
            {
                client.IsNew = false;
                await _clientRepository.UpdateAsync(client);
            }

            var session = await _sessionRepository.GetByIdAsync(request.SessionId);
            if (session == null)
            {
                return new BookingCreateResult { Success = false, ErrorMessage = "Session not found." };
            }

            var activeBookings = (await _bookingRepository.GetBookingsBySessionAsync(request.SessionId))
                .Count(b => b.Status != "Cancelled");

            if (!session.IsOpen || activeBookings >= session.Capacity)
            {
                // TODO: wire this up to the waitlist feature once it exists.
                return new BookingCreateResult { Success = false, RequiresWaitlist = true };
            }

            var cancellationToken = CancellationTokenGenerator.Generate();

            var booking = new Booking
            {
                ClientId = client.ClientId,
                SessionId = request.SessionId,
                BookingDate = DateTime.UtcNow,
                Status = "Awaiting Payment",
                CancellationToken = cancellationToken
            };
            await _bookingRepository.CreateBookingAsync(booking);

            if (isNewClient)
            {
                var intakeForm = new IntakeForm
                {
                    BookingId = booking.BookingId,
                    MedicalNotes = request.MedicalNotes ?? string.Empty,
                    ConsentSigned = request.ConsentSigned,
                    SubmittedAt = DateTime.UtcNow
                };
                _context.IntakeForms.Add(intakeForm);
                await _context.SaveChangesAsync();
            }

            return new BookingCreateResult
            {
                Success = true,
                BookingId = booking.BookingId,
                CancellationToken = cancellationToken
            };
        }

        public async Task<CancelBookingResult> CancelBookingAsync(string cancellationToken)
        {
            var booking = await _bookingRepository.GetByCancellationTokenAsync(cancellationToken);
            if (booking == null)
            {
                return new CancelBookingResult { Success = false, ErrorMessage = "Invalid cancellation link." };
            }

            if (booking.Status == "Cancelled")
            {
                return new CancelBookingResult { Success = false, ErrorMessage = "This booking has already been cancelled." };
            }

            booking.Status = "Cancelled";
            await _bookingRepository.UpdateAsync(booking);

            return new CancelBookingResult { Success = true };
        }

        public Task<Booking?> GetByIdAsync(int bookingId)
        {
            return _bookingRepository.GetByIdAsync(bookingId);
        }
    }
}

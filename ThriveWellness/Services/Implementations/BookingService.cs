using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;
using ThriveWellness.Services;

namespace ThriveWellness.Services.Implementations
{
    public class BookingService : IBookingService
    {
        // FR-06 pricing tiers.
        private const decimal PerClassPrice = 120m;
        private const decimal MonthlyPrice = 450m;

        private readonly IClientRepository _clientRepository;
        private readonly IBookingRepository _bookingRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IWaitlistService _waitlistService;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ApplicationDbContext _context;

        public BookingService(
            IClientRepository clientRepository,
            IBookingRepository bookingRepository,
            ISessionRepository sessionRepository,
            IWaitlistService waitlistService,
            IPaymentRepository paymentRepository,
            ApplicationDbContext context)
        {
            _clientRepository = clientRepository;
            _bookingRepository = bookingRepository;
            _sessionRepository = sessionRepository;
            _waitlistService = waitlistService;
            _paymentRepository = paymentRepository;
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
                // The client (found or just created above) already exists at
                // this point, so the caller can join them to the waitlist
                // without repeating the lookup/creation logic.
                return new BookingCreateResult { Success = false, RequiresWaitlist = true, ClientId = client.ClientId };
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

            var payment = new Payment
            {
                BookingId = booking.BookingId,
                Method = request.Method,
                Amount = GetAmountForPaymentType(request.PaymentType),
                Status = "Pending",
                PaymentType = request.PaymentType
            };
            await _paymentRepository.CreateAsync(payment);

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
                ClientId = client.ClientId,
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

            await _waitlistService.PromoteNextInLineAsync(booking.SessionId);

            return new CancelBookingResult { Success = true };
        }

        public Task<Booking?> GetByIdAsync(int bookingId)
        {
            return _bookingRepository.GetByIdAsync(bookingId);
        }

        private static decimal GetAmountForPaymentType(string paymentType)
        {
            return paymentType switch
            {
                "monthly" => MonthlyPrice,
                "per-class" => PerClassPrice,
                _ => throw new ArgumentException($"Unknown payment type: {paymentType}")
            };
        }
    }
}

using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IBookingService
    {
        Task<ClientStatusResult> CheckClientStatusAsync(string email);
        Task<BookingCreateResult> CreateBookingAsync(BookingRequest request);
        Task<CancelBookingResult> CancelBookingAsync(string cancellationToken);
        Task<Booking?> GetByIdAsync(int bookingId);
    }
}

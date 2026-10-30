using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IBookingService
    {
        Task<bool> IsFirstBookingAsync(int clientId);
        Task<BookingCreateResult> CreateBookingAsync(BookingRequest request);
        Task<CancelBookingResult> CancelBookingAsync(string cancellationToken);
        Task<CancelBookingResult> CancelByAdminAsync(int bookingId);
        Task<CancelBookingPreview> GetClientCancellationAsync(int bookingId, int clientId);
        Task<CancelBookingResult> CancelByClientAsync(int bookingId, int clientId);
        Task<Booking?> GetByIdAsync(int bookingId);
        Task<IReadOnlyList<ClientBookingViewModel>> GetClientBookingsAsync(int clientId);
    }
}

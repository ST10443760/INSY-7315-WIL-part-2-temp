using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    public interface IPaymentRepository
    {
        Task CreateAsync(Payment payment);
        Task<Payment?> GetByIdAsync(int id);
        Task<Payment?> GetByBookingIdAsync(int bookingId);
    }
}

using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    // Repository pattern: the only place that queries or writes the Payments
    // table directly.
    public interface IPaymentRepository
    {
        Task CreateAsync(Payment payment);
        Task<Payment?> GetByIdAsync(int id);
        Task<Payment?> GetByBookingIdAsync(int bookingId);

        // status: null/empty for every payment, or an exact Payment.Status
        // value ("Pending"/"Confirmed") to filter to just one.
        Task<IEnumerable<PaymentOverviewViewModel>> GetAllPaymentsAsync(string? status);
        Task UpdateStatusAsync(int paymentId, string status);
    }
}

using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    // Repository pattern: the only place that queries or writes the
    // PaymentTransactions table directly.
    public interface IPaymentTransactionRepository
    {
        Task AddAsync(PaymentTransaction transaction);
        Task UpdateAsync(PaymentTransaction transaction);

        // Every attempt for one payment, oldest first.
        Task<IReadOnlyList<PaymentTransaction>> GetByPaymentIdAsync(int paymentId);

        // The charge that paid this payment, if any - the one a receipt
        // shows. Still returned once it's been refunded (its Status then
        // says so); declined attempts never are.
        Task<PaymentTransaction?> GetChargeForPaymentAsync(int paymentId);
    }
}

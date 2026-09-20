using ThriveWellness.Models;

namespace ThriveWellness.Repositories.Interfaces
{
    public interface IPaymentRepository
    {
        Task CreateAsync(Payment payment);
    }
}

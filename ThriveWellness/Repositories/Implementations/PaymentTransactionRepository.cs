using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;

namespace ThriveWellness.Repositories.Implementations
{
    // Repository pattern: thin EF Core wrapper around the PaymentTransactions
    // table - no business rules here, just queries and saves.
    public class PaymentTransactionRepository : IPaymentTransactionRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentTransactionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(PaymentTransaction transaction)
        {
            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(PaymentTransaction transaction)
        {
            _context.PaymentTransactions.Update(transaction);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<PaymentTransaction>> GetByPaymentIdAsync(int paymentId)
        {
            return await _context.PaymentTransactions
                .Where(t => t.PaymentId == paymentId)
                .OrderBy(t => t.CreatedAt).ThenBy(t => t.PaymentTransactionId)
                .ToListAsync();
        }

        public async Task<PaymentTransaction?> GetChargeForPaymentAsync(int paymentId)
        {
            return await _context.PaymentTransactions
                .Where(t => t.PaymentId == paymentId && t.Status != PaymentTransactionStatus.Declined)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();
        }
    }
}

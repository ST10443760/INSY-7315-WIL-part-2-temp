namespace ThriveWellness.Services
{
    public class PaymentConfirmedEventArgs : EventArgs
    {
        public int BookingId { get; }
        public int ClientId { get; }

        public PaymentConfirmedEventArgs(int bookingId, int clientId)
        {
            BookingId = bookingId;
            ClientId = clientId;
        }
    }
}

using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: the only place that talks to SendGrid directly. Every
    // other email-sending code goes through NotificationService, which
    // builds the HTML and calls this just to transmit it - keeping the
    // SendGrid-specific API surface (and the one place a future provider
    // swap would touch) isolated to this one class.
    public class EmailSender : IEmailSender
    {
        private readonly string _apiKey;
        private readonly string _fromEmail;

        // Fails fast at startup if SendGrid isn't configured, rather than
        // failing later on the first booking confirmation a real client is
        // waiting on.
        public EmailSender(IConfiguration configuration)
        {
            _apiKey = configuration["SendGridApiKey"]
                ?? throw new InvalidOperationException("SendGridApiKey is not configured.");
            _fromEmail = configuration["SendGridFromEmail"]
                ?? throw new InvalidOperationException("SendGridFromEmail is not configured.");
        }

        // Sends a single HTML email via SendGrid. Throws on any error
        // response rather than swallowing it, so a failed send surfaces as a
        // visible error instead of a client silently never getting their
        // confirmation email (see the walkthrough for what happens today if
        // SendGrid itself is down).
        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, "Thrive Wellness Pilates");
            var to = new EmailAddress(toEmail);
            var message = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: string.Empty, htmlContent: htmlBody);

            var response = await client.SendEmailAsync(message);
            if ((int)response.StatusCode >= 400)
            {
                var body = await response.Body.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"SendGrid returned {(int)response.StatusCode} sending to {toEmail}: {body}");
            }
        }
    }
}

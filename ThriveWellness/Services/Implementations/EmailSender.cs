using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    public class EmailSender : IEmailSender
    {
        private readonly string _apiKey;
        private readonly string _fromEmail;

        public EmailSender(IConfiguration configuration)
        {
            _apiKey = configuration["SendGridApiKey"]
                ?? throw new InvalidOperationException("SendGridApiKey is not configured.");
            _fromEmail = configuration["SendGridFromEmail"]
                ?? throw new InvalidOperationException("SendGridFromEmail is not configured.");
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, "Thrive Wellness");
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

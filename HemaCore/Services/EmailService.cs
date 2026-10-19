
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace HemaCore.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private async Task SendEmailAsync(MimeMessage message)
        {
            var smtpHost = _configuration["EmailSettings:SmtpHost"];
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]!);
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                smtpHost,
                smtpPort,
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(username, password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        private MimeMessage CreateMessage(
            string recipientName,
            string recipientEmail,
            string subject)
        {
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"];

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(recipientName, recipientEmail));
            message.Subject = subject;

            return message;
        }

        public async Task SendWelcomeEmailAsync(
            string email,
            string name,
            string surname,
            string temporaryPassword,
            string role)
        {
            var message = CreateMessage(
                name, email, "Welcome to HemaCore");

            message.Body = new TextPart("html")
            {
                Text = $@"
                <html><body>
                    <h2>Welcome to HemaCore</h2>
                    <p>Hello <strong>{System.Net.WebUtility.HtmlEncode(name)} {System.Net.WebUtility.HtmlEncode(surname)}</strong>,</p>
                    <p>Your HemaCore account has been created.</p>
                    <p><strong>Role:</strong> {System.Net.WebUtility.HtmlEncode(role)}</p>
                    <p><strong>Username:</strong> {System.Net.WebUtility.HtmlEncode(email)}</p>
                    <p><strong>Temporary Password:</strong> {System.Net.WebUtility.HtmlEncode(temporaryPassword)}</p>
                    <p>Please log in and change your password after your first login.</p>
                    <p>Regards,<br /><strong>HemaCore</strong></p>
                </body></html>"
            };

            await SendEmailAsync(message);
        }

        public async Task SendConsentGrantedEmailAsync(
            string doctorEmail,
            string doctorName,
            string patientName,
            string patientSurname,
            List<string> sharedConditions)
        {
            var conditionsHtml = sharedConditions != null && sharedConditions.Any()
                ? string.Join("", sharedConditions.Select(condition =>
                    $"<li>{System.Net.WebUtility.HtmlEncode(condition)}</li>"))
                : "<li>No medical conditions were shared.</li>";

            var message = CreateMessage(
                doctorName, doctorEmail, "Patient Consent Granted - HemaCore");

            message.Body = new TextPart("html")
            {
                Text = $@"
                <html><body>
                    <h2>Patient Consent Granted</h2>
                    <p>Hello <strong>{System.Net.WebUtility.HtmlEncode(doctorName)}</strong>,</p>
                    <p><strong>{System.Net.WebUtility.HtmlEncode(patientName)} {System.Net.WebUtility.HtmlEncode(patientSurname)}</strong>
                    has granted you consent to access selected medical information through HemaCore.</p>
                    <h3>Information shared with you:</h3>
                    <ul>{conditionsHtml}</ul>
                    <p>Please log in to HemaCore to view the shared information.</p>
                    <p>Regards,<br /><strong>HemaCore</strong></p>
                </body></html>"
            };

            await SendEmailAsync(message);
        }

        public async Task SendConsentRevokedEmailAsync(
            string doctorEmail,
            string doctorName,
            string patientName,
            string patientSurname)
        {
            var message = CreateMessage(
                doctorName, doctorEmail, "Patient Consent Revoked - HemaCore");

            message.Body = new TextPart("html")
            {
                Text = $@"
                <html><body>
                    <h2>Patient Consent Revoked</h2>
                    <p>Hello <strong>{System.Net.WebUtility.HtmlEncode(doctorName)}</strong>,</p>
                    <p><strong>{System.Net.WebUtility.HtmlEncode(patientName)} {System.Net.WebUtility.HtmlEncode(patientSurname)}</strong>
                    has revoked the consent previously granted through HemaCore.</p>
                    <p>You no longer have consent to access the medical information shared through this consent.</p>
                    <p>Please log in to HemaCore to view the current access status.</p>
                    <p>Regards,<br /><strong>HemaCore</strong></p>
                </body></html>"
            };

            await SendEmailAsync(message);
        }

        public async Task SendResultsReleasedEmailAsync(
            string patientEmail,
            string patientName,
            string patientSurname,
            int testRequestId,
            int verifiedResultCount)
        {
            var message = CreateMessage(
                $"{patientName} {patientSurname}",
                patientEmail,
                $"Your test results are ready (Request #{testRequestId}) - HemaCore");

            var resultWord = verifiedResultCount == 1 ? "result" : "results";

            message.Body = new TextPart("html")
            {
                Text = $@"
                <html><body>
                    <h2>Your Test Results Are Ready</h2>
                    <p>Hello <strong>{System.Net.WebUtility.HtmlEncode(patientName)} {System.Net.WebUtility.HtmlEncode(patientSurname)}</strong>,</p>
                    <p>Your doctor has released <strong>{verifiedResultCount}</strong> verified {resultWord} for test request <strong>#{testRequestId}</strong>.</p>
                    <p>Please log in to the HemaCore patient portal and open Results to view them.</p>
                    <p>If you have questions about your results, please contact your doctor.</p>
                    <p>Regards,<br /><strong>HemaCore</strong></p>
                </body></html>"
            };

            await SendEmailAsync(message);
        }

        public async Task SendVerifiedResultsEmailAsync(
            string doctorEmail,
            string doctorName,
            string patientName,
            int testRequestId,
            byte[] pdfBytes)
        {
            if (string.IsNullOrWhiteSpace(doctorEmail))
            {
                throw new InvalidOperationException(
                    "The requesting doctor's email address is missing.");
            }

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                throw new InvalidOperationException(
                    "The test results PDF is empty.");
            }

            var message = CreateMessage(
                doctorName,
                doctorEmail,
                $"Verified Laboratory Results - Request #{testRequestId}");

            var body = new BodyBuilder
            {
                HtmlBody = $@"
                <html><body>
                    <h2>Verified Laboratory Results</h2>
                    <p>Dear Dr. {System.Net.WebUtility.HtmlEncode(doctorName)},</p>
                    <p>All test results for request <strong>#{testRequestId}</strong> for
                    <strong>{System.Net.WebUtility.HtmlEncode(patientName)}</strong>
                    have been verified by the laboratory.</p>
                    <p>The verified results are attached as a PDF for your review.</p>
                    <p>Please review the results in HemaCore and release them to the patient when appropriate.</p>
                    <p>Regards,<br /><strong>HemaCore</strong></p>
                </body></html>"
            };

            body.Attachments.Add(
                $"VerifiedResults_Request_{testRequestId}.pdf",
                pdfBytes,
                new ContentType("application", "pdf"));

            message.Body = body.ToMessageBody();

            await SendEmailAsync(message);
        }
    }
}

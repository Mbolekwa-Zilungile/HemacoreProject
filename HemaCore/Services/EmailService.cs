
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HemaCore.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string GetRequiredSetting(string key)
        {
            var value = _configuration[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Missing email configuration setting: {key}");
            }

            return value;
        }

        private async Task SendEmailAsync(MimeMessage message)
        {
            var smtpHost = GetRequiredSetting("EmailSettings:SmtpHost");
            var smtpPortText = GetRequiredSetting("EmailSettings:SmtpPort");
            var username = GetRequiredSetting("EmailSettings:Username");
            var password = GetRequiredSetting("EmailSettings:Password");

            if (!int.TryParse(smtpPortText, out var smtpPort))
            {
                throw new InvalidOperationException(
                    "EmailSettings:SmtpPort must be a valid port number.");
            }

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                smtpHost,
                smtpPort,
                SecureSocketOptions.StartTls);

            try
            {
                await smtp.AuthenticateAsync(username, password);
                await smtp.SendAsync(message);
            }
            finally
            {
                if (smtp.IsConnected)
                {
                    await smtp.DisconnectAsync(true);
                }
            }
        }

        private MimeMessage CreateMessage(
            string recipientName,
            string recipientEmail,
            string subject)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new InvalidOperationException(
                    "The recipient email address is missing.");
            }

            var senderEmail =
                GetRequiredSetting("EmailSettings:SenderEmail");

            var senderName =
                _configuration["EmailSettings:SenderName"] ?? "HemaCore";

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(senderName, senderEmail));

            message.To.Add(
                new MailboxAddress(
                    recipientName ?? string.Empty,
                    recipientEmail));

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
                $"{name} {surname}",
                email,
                "Welcome to HemaCore");

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

        public async Task SendPasswordResetEmailAsync(
            string email,
            string resetLink)
        {
            if (string.IsNullOrWhiteSpace(resetLink))
            {
                throw new ArgumentException(
                    "The password reset link is required.",
                    nameof(resetLink));
            }

            if (!Uri.TryCreate(resetLink, UriKind.Absolute, out var resetUri) ||
                (resetUri.Scheme != Uri.UriSchemeHttps &&
                 resetUri.Scheme != Uri.UriSchemeHttp))
            {
                throw new ArgumentException(
                    "The password reset link must be a valid HTTP or HTTPS URL.",
                    nameof(resetLink));
            }

            var message = CreateMessage(
                email,
                email,
                "Reset Your HemaCore Password");

            var safeResetLink =
                System.Net.WebUtility.HtmlEncode(resetLink);

            message.Body = new TextPart("html")
            {
                Text = $@"
                <html><body>
                    <h2>Password Reset Request</h2>
                    <p>We received a request to reset your HemaCore password.</p>
                    <p>
                        <a href=""{safeResetLink}"">
                            Reset My Password
                        </a>
                    </p>
                    <p>If you did not request this, you can ignore this email.</p>
                    <p>For your security, use the reset link only once and do not share it.</p>
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
            var conditionsHtml =
                sharedConditions != null && sharedConditions.Any()
                ? string.Join("", sharedConditions.Select(condition =>
                    $"<li>{System.Net.WebUtility.HtmlEncode(condition)}</li>"))
                : "<li>No medical conditions were shared.</li>";

            var message = CreateMessage(
                doctorName,
                doctorEmail,
                "Patient Consent Granted - HemaCore");

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
                doctorName,
                doctorEmail,
                "Patient Consent Revoked - HemaCore");

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

            var resultWord = verifiedResultCount == 1
                ? "result"
                : "results";

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

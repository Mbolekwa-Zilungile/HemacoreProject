using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Linq;

namespace HemaCore.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendWelcomeEmailAsync(
            string email,
            string name,
            string surname,
            string temporaryPassword,
            string role)
        {
            var smtpHost = _configuration["EmailSettings:SmtpHost"];
            var smtpPort = int.Parse(
                _configuration["EmailSettings:SmtpPort"]!
            );

            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"];

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(senderName, senderEmail)
            );

            message.To.Add(
                new MailboxAddress(name, email)
            );

            message.Subject = "Welcome to HemaCore";

            message.Body = new TextPart("html")
            {
                Text = $@"
                    <html>
                    <body>

                        <h2>Welcome to HemaCore</h2>

                        <p>Hello <strong>{surname +name}</strong>,</p>

                        <p>
                            Your HemaCore account has been successfully
                            created.
                        </p>

                        <p>
                            <strong>Role:</strong> {role}
                        </p>

                        <p>
                            <strong>Username:</strong> {email}
                        </p>

                        <p>
                            <strong>Temporary Password:</strong>
                            {temporaryPassword}
                        </p>

                        <p>
                            Please log in using these details and
                            change your password after your first login.
                        </p>

                        <br />

                        <p>
                            Regards,<br />
                            <strong>HemaCore</strong>
                        </p>

                    </body>
                    </html>
                "
            };

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                smtpHost,
                smtpPort,
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                username,
                password
            );

            await smtp.SendAsync(message);

            await smtp.DisconnectAsync(true);
        }
       
public async Task SendConsentGrantedEmailAsync(
    string doctorEmail,
    string doctorName,
    string patientName,
    string patientSurname,
    List<string> sharedConditions)
        {
            var smtpHost = _configuration["EmailSettings:SmtpHost"];

            var smtpPort = int.Parse(
                _configuration["EmailSettings:SmtpPort"]!
            );

            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderName = _configuration["EmailSettings:SenderName"];

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress(senderName, senderEmail)
            );

            message.To.Add(
                new MailboxAddress(doctorName, doctorEmail)
            );

            message.Subject = "Patient Consent Granted - HemaCore";

            string conditionsHtml;

            if (sharedConditions != null && sharedConditions.Any())
            {
                conditionsHtml = string.Join(
                    "",
                    sharedConditions.Select(condition =>
                        $"<li>{condition}</li>")
                );
            }
            else
            {
                conditionsHtml = "<li>No medical conditions were shared.</li>";
            }

            message.Body = new TextPart("html")
            {
                Text = $@"
            <html>
            <body>
                <h2>Patient Consent Granted</h2>

                <p>Hello <strong>{doctorName}</strong>,</p>

                <p>
                    <strong>{patientName} {patientSurname}</strong>
                    has granted you consent to access selected
                    medical information through HemaCore.
                </p>

                <h3>Information shared with you:</h3>

                <ul>
                    {conditionsHtml}
                </ul>

                <p>
                    Please log in to HemaCore to view the information
                    that has been shared with you.
                </p>

                <br />

                <p>
                    Regards,<br />
                    <strong>HemaCore</strong>
                </p>
            </body>
            </html>
        "
            };

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                smtpHost,
                smtpPort,
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(username, password);

            await smtp.SendAsync(message);

            await smtp.DisconnectAsync(true);
        }


    }
}
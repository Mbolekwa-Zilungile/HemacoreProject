using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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
    }
}
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace HemaCore.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendDoctorCredentialsAsync(
            string doctorEmail,
            string doctorName,
            string temporaryPassword)
        {
            var email = new MimeMessage();

            // Sender
            email.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]
            ));

            // Doctor
            email.To.Add(new MailboxAddress(
                doctorName,
                doctorEmail
            ));

            // Subject
            email.Subject = "HemaCore Doctor Account";

            // Email body
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>

                    <h2>Welcome to HemaCore</h2>

                    <p>Dear Dr. {doctorName},</p>

                    <p>
                        Your doctor account has been created
                        on the HemaCore system.
                    </p>

                    <h3>Login Details</h3>

                    <p>
                        <strong>Email:</strong> {doctorEmail}
                    </p>

                    <p>
                        <strong>Temporary Password:</strong>
                        {temporaryPassword}
                    </p>

                    <p>
                        Please use the above credentials to log in.
                    </p>

                    <p>
                        For security purposes, please change your
                        password after your first login.
                    </p>

                    <br>

                    <p>Regards,</p>
                    <p><strong>HemaCore</strong></p>

                </body>
                </html>
            ";

            email.Body = new BodyBuilder
            {
                HtmlBody = body
            }.ToMessageBody();

            // SMTP
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                _configuration["EmailSettings:SmtpServer"],
                int.Parse(_configuration["EmailSettings:Port"]),
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                _configuration["EmailSettings:Username"],
                _configuration["EmailSettings:Password"]
            );

            await smtp.SendAsync(email);

            await smtp.DisconnectAsync(true);
        }
    }
}


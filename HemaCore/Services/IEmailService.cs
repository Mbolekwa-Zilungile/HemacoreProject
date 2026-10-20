
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HemaCore.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(
            string email,
            string name,
            string surname,
            string temporaryPassword,
            string role);

        Task SendConsentGrantedEmailAsync(
            string doctorEmail,
            string doctorName,
            string patientName,
            string patientSurname,
            List<string> sharedConditions);

        Task SendConsentRevokedEmailAsync(
            string doctorEmail,
            string doctorName,
            string patientName,
            string patientSurname);

        Task SendVerifiedResultsEmailAsync(
            string doctorEmail,
            string doctorName,
            string patientName,
            int testRequestId,
            byte[] pdfBytes);

        Task SendResultsReleasedEmailAsync(
            string patientEmail,
            string patientName,
            string patientSurname,
            int testRequestId,
            int verifiedResultCount);

        // Sends a password-reset email to the user.
        Task SendPasswordResetEmailAsync(
            string email,
            string resetLink);
    }
}

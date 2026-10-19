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

        // NEW: notifies the patient that the doctor has released verified results.
        Task SendResultsReleasedEmailAsync(
            string patientEmail,
            string patientName,
            string patientSurname,
            int testRequestId,
            int verifiedResultCount);
    }
}
using HemaCore.Data;
using HemaCore.Models;
using HemaCore.ViewModels;
using HemaCore.ViewModels.Patient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HemaCore.Services;

namespace HemaCore.Controllers
{
    public class PatientController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        private readonly IEmailService _emailService;

        public PatientController(
            AppDbContext context,
            UserManager<Users> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }
        #region Get Current Patient
        // ---------------------------------------------------------------------
        // Helper: get the Patient record for the currently logged-in user
        // ---------------------------------------------------------------------
        private async Task<Patient?> GetCurrentPatientAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            return await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == user.Id);
        }
        #endregion

        #region Dashboard
        // ---------------------------------------------------------------------
        // Dashboard
        // ---------------------------------------------------------------------
        public async Task<IActionResult> Dashboard()
        {
            var patient = await GetCurrentPatientAsync();
            return View(patient);
        }
        #endregion

        #region Complete Profile
        // ---------------------------------------------------------------------
        // Complete Profile (GET)
        // ---------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> CompleteProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // If patient record already exists, prefill
            var existing = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            var vm = new CompleteProfileViewModel
            {
                AvailableConditions = await _context.Conditions
                    .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                AvailableAllergies = await _context.Allergies
                    .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(),
                AvailableMedications = await _context.Medications
                    .Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync()
            };

            if (existing != null)
            {
                vm.Name = existing.Name;
                vm.Surname = existing.Surname;
                vm.IDNumber = existing.IDNumber;
                vm.Gender = existing.Gender;
                vm.PhoneNumber = existing.PhoneNumber;
                vm.DateOfBirth = existing.DateOfBirth;
                vm.StreetAddress = existing.StreetAddress;
                vm.Suburb = existing.Suburb;
                vm.City = existing.City;
                vm.Province = existing.Province;
                vm.PostalCode = existing.PostalCode;

                vm.SelectedConditionIds = await _context.PatientConditions
                    .Where(pc => pc.PatientID == existing.PatientID)
                    .Select(pc => pc.ConditionId).ToListAsync();

                vm.SelectedAllergyIds = await _context.PatientAllergies
                    .Where(pa => pa.PatientID == existing.PatientID)
                    .Select(pa => pa.AllergyId).ToListAsync();

                vm.SelectedMedicationIds = await _context.PatientMedications
                    .Where(pm => pm.PatientID == existing.PatientID)
                    .Select(pm => pm.MedicationId).ToListAsync();
            }

            return View(vm);
        }

        
        // ---------------------------------------------------------------------
        // Complete Profile (POST)
        // ---------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(CompleteProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                model.AvailableConditions = await _context.Conditions
                    .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
                model.AvailableAllergies = await _context.Allergies
                    .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();
                model.AvailableMedications = await _context.Medications
                    .Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync();
                return View(model);
            }

            // 1. Create or update the Patient
            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null)
            {
                patient = new Patient { UserId = user.Id };
                _context.Patients.Add(patient);
            }

            patient.Name = model.Name;
            patient.Surname = model.Surname;
            patient.IDNumber = model.IDNumber;
            patient.Gender = model.Gender;
            patient.PhoneNumber = model.PhoneNumber;
            patient.DateOfBirth = model.DateOfBirth;
            patient.StreetAddress = model.StreetAddress;
            patient.Suburb = model.Suburb;
            patient.City = model.City;
            patient.Province = model.Province;
            patient.PostalCode = model.PostalCode;

            await _context.SaveChangesAsync();  // ensures PatientID is set

            // 2. Sync conditions
            var existingConditions = _context.PatientConditions
                .Where(pc => pc.PatientID == patient.PatientID);
            _context.PatientConditions.RemoveRange(existingConditions);

            foreach (var cid in model.SelectedConditionIds.Distinct())
            {
                _context.PatientConditions.Add(new PatientCondition
                {
                    PatientID = patient.PatientID,
                    ConditionId = cid
                });
            }

            // 3. Sync allergies
            var existingAllergies = _context.PatientAllergies
                .Where(pa => pa.PatientID == patient.PatientID);
            _context.PatientAllergies.RemoveRange(existingAllergies);

            foreach (var aid in model.SelectedAllergyIds.Distinct())
            {
                _context.PatientAllergies.Add(new PatientAllergy
                {
                    PatientID = patient.PatientID,
                    AllergyId = aid
                });
            }

            // 4. Sync medications
            var existingMeds = _context.PatientMedications
                .Where(pm => pm.PatientID == patient.PatientID);
            _context.PatientMedications.RemoveRange(existingMeds);

            foreach (var mid in model.SelectedMedicationIds.Distinct())
            {
                _context.PatientMedications.Add(new PatientMedication
                {
                    PatientID = patient.PatientID,
                    MedicationId = mid
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Profile saved successfully!";
            return RedirectToAction(nameof(Dashboard));
        }
#endregion

        #region Patient Details
        // ---------------------------------------------------------------------
        // Patient Details
        // ---------------------------------------------------------------------
        public async Task<IActionResult> PatientDetails()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction(nameof(CompleteProfile));

            // Eager-load the reference data so the view can display names
            var conditions = await _context.PatientConditions
                .Where(pc => pc.PatientID == patient.PatientID)
                .Include(pc => pc.Condition)
                .ToListAsync();

            var allergies = await _context.PatientAllergies
                .Where(pa => pa.PatientID == patient.PatientID)
                .Include(pa => pa.Allergy)
                .ToListAsync();

            var medications = await _context.PatientMedications
                .Where(pm => pm.PatientID == patient.PatientID)
                .Include(pm => pm.Medication)
                .ToListAsync();

            ViewBag.Conditions = conditions;
            ViewBag.Allergies = allergies;
            ViewBag.Medications = medications;

            return View(patient);
        }
#endregion

        #region Edit Profile
        // ---------------------------------------------------------------------
        // Edit Profile (GET)
        // ---------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null) return RedirectToAction(nameof(CompleteProfile));

            var vm = new EditPatientProfileViewModel
            {
                Email = user.Email ?? "",
                Name = patient.Name,
                Surname = patient.Surname,
                IDNumber = patient.IDNumber,
                PhoneNumber = patient.PhoneNumber,
                Gender = patient.Gender,
                DateOfBirth = patient.DateOfBirth,
                StreetAddress = patient.StreetAddress,
                Suburb = patient.Suburb,
                City = patient.City,
                Province = patient.Province,
                PostalCode = patient.PostalCode,

                SelectedConditionIds = await _context.PatientConditions
                    .Where(pc => pc.PatientID == patient.PatientID)
                    .Select(pc => pc.ConditionId).ToListAsync(),

                SelectedAllergyIds = await _context.PatientAllergies
                    .Where(pa => pa.PatientID == patient.PatientID)
                    .Select(pa => pa.AllergyId).ToListAsync(),

                SelectedMedicationIds = await _context.PatientMedications
                    .Where(pm => pm.PatientID == patient.PatientID)
                    .Select(pm => pm.MedicationId).ToListAsync(),

                AvailableConditions = await _context.Conditions
                    .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                AvailableAllergies = await _context.Allergies
                    .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(),
                AvailableMedications = await _context.Medications
                    .Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync()
            };

            return View(vm);
        }
#endregion

        #region Edit Profile
        // ---------------------------------------------------------------------
        // Edit Profile (POST)
        // ---------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditPatientProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                model.AvailableConditions = await _context.Conditions
                    .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
                model.AvailableAllergies = await _context.Allergies
                    .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();
                model.AvailableMedications = await _context.Medications
                    .Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync();
                return View(model);
            }

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null) return NotFound();

            // Update patient
            patient.Name = model.Name;
            patient.Surname = model.Surname;
            patient.IDNumber = model.IDNumber;
            patient.PhoneNumber = model.PhoneNumber;
            patient.Gender = model.Gender;
            patient.DateOfBirth = model.DateOfBirth;
            patient.StreetAddress = model.StreetAddress;
            patient.Suburb = model.Suburb;
            patient.City = model.City;
            patient.Province = model.Province;
            patient.PostalCode = model.PostalCode;

            // Update Identity email if changed
            if (user.Email != model.Email)
            {
                user.Email = model.Email;
                user.UserName = model.Email;
                await _userManager.UpdateAsync(user);
            }

            // Sync selected reference data
            _context.PatientConditions.RemoveRange(
                _context.PatientConditions.Where(pc => pc.PatientID == patient.PatientID));
            foreach (var cid in model.SelectedConditionIds.Distinct())
                _context.PatientConditions.Add(new PatientCondition
                {
                    PatientID = patient.PatientID,
                    ConditionId = cid
                });

            _context.PatientAllergies.RemoveRange(
                _context.PatientAllergies.Where(pa => pa.PatientID == patient.PatientID));
            foreach (var aid in model.SelectedAllergyIds.Distinct())
                _context.PatientAllergies.Add(new PatientAllergy
                {
                    PatientID = patient.PatientID,
                    AllergyId = aid
                });

            _context.PatientMedications.RemoveRange(
                _context.PatientMedications.Where(pm => pm.PatientID == patient.PatientID));
            foreach (var mid in model.SelectedMedicationIds.Distinct())
                _context.PatientMedications.Add(new PatientMedication
                {
                    PatientID = patient.PatientID,
                    MedicationId = mid
                });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(EditProfile));
        }
        #endregion

        #region Change Password
        // ---------------------------------------------------------------------
        // Change Password
        // ---------------------------------------------------------------------
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var result = await _userManager.ChangePasswordAsync(
                user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError("", err.Description);
                return View(model);
            }

            await _userManager.UpdateSecurityStampAsync(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully. Please log in again.";
            return RedirectToAction("Login", "Account");
        }
        #endregion

        #region Test Request 
        // ============================================================
        // PATIENT TEST REQUESTS
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> MyTestRequests()
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
            {
                return RedirectToAction(nameof(CompleteProfile));
            }

            var requests = await _context.TestRequests
                .Include(r => r.Doctor)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Include(r => r.Samples)
                    .ThenInclude(s => s.SampleType)
                .Where(r => r.PatientID == patient.PatientID)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }
        #endregion

        #region Results
        [HttpGet]
        public async Task<IActionResult> Result()
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
            {
                return RedirectToAction(nameof(CompleteProfile));
            }

            var requests = await _context.TestRequests
                .Include(r => r.Doctor)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestResult)
                        .ThenInclude(tr => tr.TestResultVerification)
                .Where(r =>
                    r.PatientID == patient.PatientID &&
                    r.ReleasedToPatientDate != null)
                .OrderByDescending(r => r.ReleasedToPatientDate)
                .ToListAsync();

            // Only show requests that contain at least
            // one verified result.
            requests = requests
                .Where(r => r.TestRequestTestTypes.Any(i =>
                    i.TestResult != null &&
                    i.TestResult.TestResultVerification != null &&
                    i.TestResult.TestResultVerification.Status == "Verified"))
                .ToList();

            return View(requests);
        }
        #endregion


        #region Health
        [HttpGet]
        public async Task<IActionResult> Health(string period = "all")
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
            {
                return RedirectToAction(nameof(CompleteProfile));
            }

            // ---------------------------------------------------------
            // DETERMINE DATE RANGE
            // ---------------------------------------------------------

            DateTime? fromDate = null;
            DateTime today = DateTime.Now.Date;

            switch (period?.ToLower())
            {
                case "7":
                    fromDate = today.AddDays(-7);
                    break;

                case "30":
                    fromDate = today.AddDays(-30);
                    break;

                case "90":
                    fromDate = today.AddDays(-90);
                    break;

                case "180":
                    fromDate = today.AddDays(-180);
                    break;

                case "365":
                    fromDate = today.AddDays(-365);
                    break;

                case "all":
                default:
                    fromDate = null;
                    period = "all";
                    break;
            }


            // ---------------------------------------------------------
            // GET PATIENT TEST REQUESTS
            // ---------------------------------------------------------

            var requestsQuery = _context.TestRequests
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestResult)
                .Where(r => r.PatientID == patient.PatientID)
                .AsQueryable();


            if (fromDate.HasValue)
            {
                requestsQuery = requestsQuery.Where(r =>
                    r.RequestDate.Date >= fromDate.Value.Date &&
                    r.RequestDate.Date <= today);
            }


            var requests = await requestsQuery
                .OrderBy(r => r.RequestDate)
                .ToListAsync();


            // ---------------------------------------------------------
            // GRAPH DATA
            // ---------------------------------------------------------

            var graphData = requests
                .GroupBy(r => new
                {
                    Year = r.RequestDate.Year,
                    Month = r.RequestDate.Month
                })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    Label = new DateTime(
                        g.Key.Year,
                        g.Key.Month,
                        1
                    ).ToString("MMM yyyy"),

                    Count = g.Sum(r =>
                        r.TestRequestTestTypes?.Count ?? 0)
                })
                .ToList();


            // ---------------------------------------------------------
            // SUMMARY
            // ---------------------------------------------------------

            var totalTests = requests
                .Sum(r => r.TestRequestTestTypes?.Count ?? 0);

            var completedTests = requests
                .SelectMany(r => r.TestRequestTestTypes ?? new List<TestRequestItem>())
                .Count(t => t.Status == "Completed");

            var abnormalResults = requests
                .SelectMany(r => r.TestRequestTestTypes ?? new List<TestRequestItem>())
                .Count(t =>
                    t.TestResult != null &&
                    t.TestResult.IsAbnormal);


            // ---------------------------------------------------------
            // CURRENT CONDITIONS
            // ---------------------------------------------------------

            var conditions = await _context.PatientConditions
                .Where(pc => pc.PatientID == patient.PatientID)
                .Include(pc => pc.Condition)
                .OrderBy(pc => pc.Condition.Name)
                .ToListAsync();


            // ---------------------------------------------------------
            // SEND DATA TO VIEW
            // ---------------------------------------------------------

            ViewBag.Period = period;

            ViewBag.GraphLabels = graphData
                .Select(x => x.Label)
                .ToList();

            ViewBag.GraphCounts = graphData
                .Select(x => x.Count)
                .ToList();

            ViewBag.TotalTests = totalTests;
            ViewBag.CompletedTests = completedTests;
            ViewBag.AbnormalResults = abnormalResults;

            ViewBag.Conditions = conditions;

            return View(requests);
        }
        #endregion

        #region Consent

        

        // ---------------------------------------------------------------------
        // Patient Consent (GET)
        // ---------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Consent()
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
            {
                return RedirectToAction(nameof(CompleteProfile));
            }

            // Active doctors
            var doctors = await _context.Doctors
                .Include(d => d.User)
                .Where(d => d.IsActive)
                .OrderBy(d => d.Surname)
                .ThenBy(d => d.Name)
                .ToListAsync();

            // Completed test requests belonging to this patient
            var completedTestRequests = await _context.TestRequests
                .Include(r => r.Doctor)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Where(r =>
                    r.PatientID == patient.PatientID &&
                    r.Status == "Completed")
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            // Existing consents and shared test requests
            var consents = await _context.Consents
                .Where(c => c.PatientID == patient.UserId)
                .Include(c => c.Doctor)
                .Include(c => c.TestAccesses)
                    .ThenInclude(ct => ct.TestRequest)
                        .ThenInclude(r => r.TestRequestTestTypes)
                            .ThenInclude(i => i.TestType)
                .OrderByDescending(c => c.GrantedAt)
                .ToListAsync();

            // Automatically deactivate expired consents
            var now = DateTime.UtcNow;

            var expiredConsents = consents
                .Where(c =>
                    c.IsActive &&
                    c.ExpiresAt.HasValue &&
                    c.ExpiresAt.Value <= now)
                .ToList();

            foreach (var consent in expiredConsents)
            {
                consent.IsActive = false;
            }

            if (expiredConsents.Any())
            {
                await _context.SaveChangesAsync();
            }

            ViewBag.Doctors = doctors;
            ViewBag.CompletedTestRequests = completedTestRequests;
            ViewBag.Consents = consents;

            return View();
        }


        // ---------------------------------------------------------------------
        // Patient Consent (POST)
        // ---------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Consent(
            int doctorId,
            List<int>? selectedTestRequestIds,
            string consentDuration,
            DateTime? customExpiry)
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
            {
                return RedirectToAction(nameof(CompleteProfile));
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // -------------------------------------------------------------
            // Validate doctor
            // -------------------------------------------------------------
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d =>
                    d.DoctorId == doctorId &&
                    d.IsActive);

            if (doctor == null)
            {
                TempData["Error"] =
                    "The selected doctor could not be found.";

                return RedirectToAction(nameof(Consent));
            }

            // -------------------------------------------------------------
            // Remove expired active consent before checking
            // -------------------------------------------------------------
            var now = DateTime.UtcNow;

            var existingConsent = await _context.Consents
                .FirstOrDefaultAsync(c =>
                    c.PatientID == user.Id &&
                    c.DoctorId == doctorId &&
                    c.IsActive);

            if (existingConsent != null &&
                existingConsent.ExpiresAt.HasValue &&
                existingConsent.ExpiresAt.Value <= now)
            {
                existingConsent.IsActive = false;

                await _context.SaveChangesAsync();

                existingConsent = null;
            }

            // -------------------------------------------------------------
            // Prevent duplicate active consent
            // -------------------------------------------------------------
            if (existingConsent != null)
            {
                TempData["Error"] =
                    "You already have active consent with this doctor. " +
                    "Please revoke the existing consent before creating a new one.";

                return RedirectToAction(nameof(Consent));
            }

            // -------------------------------------------------------------
            // Determine expiry date
            // -------------------------------------------------------------
            DateTime expiresAt;

            switch (consentDuration)
            {
                case "1Hour":
                    expiresAt = now.AddHours(1);
                    break;

                case "1Day":
                    expiresAt = now.AddDays(1);
                    break;

                case "7Days":
                    expiresAt = now.AddDays(7);
                    break;

                case "30Days":
                    expiresAt = now.AddDays(30);
                    break;

                case "Custom":

                    if (!customExpiry.HasValue)
                    {
                        TempData["Error"] =
                            "Please select a custom expiry date.";

                        return RedirectToAction(nameof(Consent));
                    }

                    expiresAt = customExpiry.Value;

                    if (expiresAt <= now)
                    {
                        TempData["Error"] =
                            "The expiry date must be in the future.";

                        return RedirectToAction(nameof(Consent));
                    }

                    break;

                default:

                    TempData["Error"] =
                        "Please select a valid consent duration.";

                    return RedirectToAction(nameof(Consent));
            }

            // -------------------------------------------------------------
            // Validate selected completed test requests
            // -------------------------------------------------------------
            selectedTestRequestIds ??= new List<int>();

            var validTestRequestIds = new List<int>();

            if (selectedTestRequestIds.Any())
            {
                validTestRequestIds = await _context.TestRequests
                    .Where(r =>
                        r.PatientID == patient.PatientID &&
                        r.Status == "Completed" &&
                        selectedTestRequestIds.Contains(r.TestRequestId))
                    .Select(r => r.TestRequestId)
                    .Distinct()
                    .ToListAsync();

                // If the patient submitted request IDs but none are valid,
                // reject the request rather than silently creating consent
                // without the requested test selections.
                if (!validTestRequestIds.Any())
                {
                    TempData["Error"] =
                        "The selected test requests are no longer available. " +
                        "Please refresh the page and try again.";

                    return RedirectToAction(nameof(Consent));
                }
            }

            // -------------------------------------------------------------
            // Create consent
            // -------------------------------------------------------------
            var consent = new Consent
            {
                PatientID = user.Id,
                DoctorId = doctorId,
                IsActive = true,
                GrantedAt = now,
                ExpiresAt = expiresAt,
                RevokedAt = null
            };

            _context.Consents.Add(consent);

            await _context.SaveChangesAsync();

            // -------------------------------------------------------------
            // Save selected test requests
            // -------------------------------------------------------------
            foreach (var testRequestId in validTestRequestIds)
            {
                consent.TestAccesses.Add(new ConsentTestRequest
                {
                    ConsentId = consent.Id,
                    TestRequestId = testRequestId
                });
            }

            await _context.SaveChangesAsync();

            // -------------------------------------------------------------
            // Get shared test request names for the notification
            // -------------------------------------------------------------
            var sharedTestRequests = await _context.TestRequests
                .Where(r => validTestRequestIds.Contains(r.TestRequestId))
                .Select(r => "Test Request #" + r.TestRequestId)
                .ToListAsync();

            // -------------------------------------------------------------
            // Email doctor
            // -------------------------------------------------------------
            var doctorEmail = doctor.User?.Email;

            if (!string.IsNullOrWhiteSpace(doctorEmail))
            {
                await _emailService.SendConsentGrantedEmailAsync(
                    doctorEmail,
                    doctor.Name,
                    patient.Name,
                    patient.Surname,
                    sharedTestRequests);
            }

            TempData["Success"] =
                $"Consent granted to Dr. {doctor.Name} {doctor.Surname} " +
                $"until {expiresAt.ToLocalTime():dd MMM yyyy HH:mm}.";

            return RedirectToAction(nameof(Consent));
        }


        // =============================================================
        // DOCTOR CONSENT REQUESTS - PATIENT SIDE
        // =============================================================

        [HttpGet]
        public async Task<IActionResult> ConsentRequests()
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
                return RedirectToAction(nameof(CompleteProfile));

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            // Load requests sent to this patient by doctors.
            var requests = await _context.ConsentRequests
                .Include(r => r.Doctor)
                    .ThenInclude(d => d.User)
                .Where(r => r.PatientID == user.Id)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            // Only completed test requests belonging to this patient
            // can be selected for sharing.
            var completedTestRequests = await _context.TestRequests
                .Include(r => r.Doctor)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Where(r =>
                    r.PatientID == patient.PatientID &&
                    r.Status == "Completed")
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            // Deactivate expired consent before displaying requests.
            var now = DateTime.UtcNow;

            var expiredConsents = await _context.Consents
                .Where(c =>
                    c.PatientID == user.Id &&
                    c.IsActive &&
                    c.ExpiresAt.HasValue &&
                    c.ExpiresAt.Value <= now)
                .ToListAsync();

            foreach (var consent in expiredConsents)
                consent.IsActive = false;

            if (expiredConsents.Count > 0)
                await _context.SaveChangesAsync();

            ViewBag.CompletedTestRequests = completedTestRequests;

            return View(requests);
        }


        // =============================================================
        // PATIENT APPROVES A DOCTOR'S CONSENT REQUEST
        // =============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveConsentRequest(
            int requestId,
            List<int>? selectedTestRequestIds,
            string consentDuration,
            DateTime? customExpiry)
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
                return RedirectToAction(nameof(CompleteProfile));

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var request = await _context.ConsentRequests
                .Include(r => r.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(r =>
                    r.Id == requestId &&
                    r.PatientID == user.Id &&
                    r.Status == "Pending");

            if (request == null)
            {
                TempData["Error"] =
                    "This request could not be found or has already been answered.";

                return RedirectToAction(nameof(ConsentRequests));
            }

            if (!request.Doctor.IsActive)
            {
                TempData["Error"] =
                    "This doctor is no longer active. Consent was not granted.";

                return RedirectToAction(nameof(ConsentRequests));
            }

            var now = DateTime.UtcNow;

            // Prevent a second active consent for the same doctor.
            var existingConsents = await _context.Consents
                .Where(c =>
                    c.PatientID == user.Id &&
                    c.DoctorId == request.DoctorId &&
                    c.IsActive)
                .ToListAsync();

            foreach (var existing in existingConsents)
            {
                if (existing.ExpiresAt.HasValue &&
                    existing.ExpiresAt.Value <= now)
                {
                    existing.IsActive = false;
                }
                else
                {
                    TempData["Error"] =
                        "You already have active consent with this doctor. " +
                        "Revoke the existing consent before granting another.";

                    return RedirectToAction(nameof(ConsentRequests));
                }
            }

            // Determine the consent expiry.
            DateTime expiresAt;

            switch (consentDuration)
            {
                case "2Days":
                    expiresAt = now.AddDays(2);
                    break;

                case "7Days":
                    expiresAt = now.AddDays(7);
                    break;

                case "30Days":
                    expiresAt = now.AddDays(30);
                    break;

                case "Custom":
                    if (!customExpiry.HasValue)
                    {
                        TempData["Error"] =
                            "Please select a custom expiry date and time.";

                        return RedirectToAction(nameof(ConsentRequests));
                    }

                    // datetime-local values are normally received as
                    // unspecified/local time. Convert them to UTC.
                    var localExpiry = DateTime.SpecifyKind(
                        customExpiry.Value,
                        DateTimeKind.Local);

                    if (localExpiry < DateTime.Now.AddDays(2))
                    {
                        TempData["Error"] =
                            "Custom consent must last at least 2 days.";

                        return RedirectToAction(nameof(ConsentRequests));
                    }

                    expiresAt = localExpiry.ToUniversalTime();
                    break;

                default:
                    TempData["Error"] =
                        "Please select a valid consent duration.";

                    return RedirectToAction(nameof(ConsentRequests));
            }

            // Validate that at least one selected test request is
            // completed and belongs to the logged-in patient.
            selectedTestRequestIds ??= new List<int>();

            var validTestRequestIds = await _context.TestRequests
                .Where(r =>
                    r.PatientID == patient.PatientID &&
                    r.Status == "Completed" &&
                    selectedTestRequestIds.Contains(r.TestRequestId))
                .Select(r => r.TestRequestId)
                .Distinct()
                .ToListAsync();

            //if (!validTestRequestIds.Any())
            //{
            //    TempData["Error"] =
            //        "Select at least one completed test request to share.";

            //    return RedirectToAction(nameof(ConsentRequests));
            //}

            // Create consent only after the patient approves.
            var consent = new Consent
            {
                PatientID = user.Id,
                DoctorId = request.DoctorId,
                IsActive = true,
                GrantedAt = now,
                ExpiresAt = expiresAt,
                RevokedAt = null
            };

            _context.Consents.Add(consent);

            foreach (var testRequestId in validTestRequestIds)
            {
                consent.TestAccesses.Add(new ConsentTestRequest
                {
                    TestRequestId = testRequestId
                });
            }

            request.Status = "Approved";
            request.RespondedAt = now;

            await _context.SaveChangesAsync();

            // Notify the doctor that the patient approved the request.
            var doctorEmail = request.Doctor.User?.Email;

            if (!string.IsNullOrWhiteSpace(doctorEmail))
            {
                var sharedTestRequests = validTestRequestIds
                    .Select(id => "Test Request #" + id)
                    .ToList();

                await _emailService.SendConsentGrantedEmailAsync(
                    doctorEmail,
                    request.Doctor.Name,
                    patient.Name,
                    patient.Surname,
                    sharedTestRequests);
            }

            TempData["Success"] =
                $"Consent approved until {expiresAt.ToLocalTime():dd MMM yyyy HH:mm}.";

            return RedirectToAction(nameof(ConsentRequests));
        }


        // =============================================================
        // PATIENT REJECTS A DOCTOR'S CONSENT REQUEST
        // =============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectConsentRequest(int requestId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var request = await _context.ConsentRequests
                .FirstOrDefaultAsync(r =>
                    r.Id == requestId &&
                    r.PatientID == user.Id &&
                    r.Status == "Pending");

            if (request == null)
            {
                TempData["Error"] =
                    "This request could not be found or has already been answered.";

                return RedirectToAction(nameof(ConsentRequests));
            }

            request.Status = "Rejected";
            request.RespondedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "The doctor's consent request has been rejected. No access was granted.";

            return RedirectToAction(nameof(ConsentRequests));
        }

        // ---------------------------------------------------------------------
        // Revoke Consent
        // Keep your existing RevokeConsent action here unchanged.
        // ---------------------------------------------------------------------

        

        [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RevokeConsent(int consentId)
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
                return RedirectToAction(nameof(CompleteProfile));

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var consent = await _context.Consents
                .Include(c => c.Doctor)
                .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(c =>
                    c.Id == consentId &&
                    c.PatientID == user.Id &&
                    c.IsActive);

            if (consent == null)
            {
                TempData["Error"] =
                    "The consent could not be found or has already been revoked.";

                return RedirectToAction(nameof(Consent));
            }

            // Revoke consent
            consent.IsActive = false;
            consent.RevokedAt = DateTime.UtcNow;

            // Save the revoke first
            await _context.SaveChangesAsync();

            // Get doctor's email
            var doctorEmail = consent.Doctor.User?.Email;

            // Send revoke notification
            if (!string.IsNullOrWhiteSpace(doctorEmail))
            {
                await _emailService.SendConsentRevokedEmailAsync(
                    doctorEmail,
                    consent.Doctor.Name,
                    patient.Name,
                    patient.Surname
                );
            }

            TempData["Success"] =
                $"Consent with Dr. {consent.Doctor.Name} {consent.Doctor.Surname} has been revoked.";

            return RedirectToAction(nameof(Consent));
        }


        #endregion


        #region Reports

        [HttpGet]
        public async Task<IActionResult> Report()
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
            {
                return RedirectToAction(nameof(CompleteProfile));
            }

            var reports = await _context.PatientReports
                .Where(r => r.PatientId == patient.PatientID)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reports);
        }

        #endregion
        



        // ---------------------------------------------------------------------
        // Stub pages
        // ---------------------------------------------------------------------
        public IActionResult Tests() => View();
       // public IActionResult Result() => View();
       // public IActionResult Health() => View();
        //public IActionResult Consent() => View();
        //public IActionResult Report() => View();
        public IActionResult Index() => View();
    }
}
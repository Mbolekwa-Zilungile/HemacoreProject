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
                vm.Address = existing.Address;

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
            patient.Address = model.Address ?? "";

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
                Address = patient.Address,

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
            patient.Address = model.Address ?? "";

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

            // -------------------------------------------------------------
            // Active doctors
            // -------------------------------------------------------------
            var doctors = await _context.Doctors
                .Include(d => d.User)
                .Where(d => d.IsActive)
                .OrderBy(d => d.Surname)
                .ThenBy(d => d.Name)
                .ToListAsync();


            // -------------------------------------------------------------
            // Patient conditions
            // -------------------------------------------------------------
            var conditions = await _context.PatientConditions
                .Where(pc => pc.PatientID == patient.PatientID)
                .Include(pc => pc.Condition)
                .Where(pc => pc.Condition != null &&
                             pc.Condition.IsActive)
                .OrderBy(pc => pc.Condition.Name)
                .ToListAsync();


            // -------------------------------------------------------------
            // Patient allergies
            // -------------------------------------------------------------
            var allergies = await _context.PatientAllergies
                .Where(pa => pa.PatientID == patient.PatientID)
                .Include(pa => pa.Allergy)
                .Where(pa => pa.Allergy != null &&
                             pa.Allergy.IsActive)
                .OrderBy(pa => pa.Allergy.Name)
                .ToListAsync();


            // -------------------------------------------------------------
            // Patient medications
            // -------------------------------------------------------------
            var medications = await _context.PatientMedications
                .Where(pm => pm.PatientID == patient.PatientID)
                .Include(pm => pm.Medication)
                .Where(pm => pm.Medication != null &&
                             pm.Medication.IsActive)
                .OrderBy(pm => pm.Medication.Name)
                .ToListAsync();


            // -------------------------------------------------------------
            // Existing consents
            // -------------------------------------------------------------
            var consents = await _context.Consents
                .Where(c => c.PatientID == patient.UserId)
                .Include(c => c.Doctor)
                .Include(c => c.Conditions)
                    .ThenInclude(cc => cc.Condition)
                .Include(c => c.Allergies)
                    .ThenInclude(ca => ca.Allergy)
                .Include(c => c.Medications)
                    .ThenInclude(cm => cm.Medication)
                .OrderByDescending(c => c.GrantedAt)
                .ToListAsync();


            // Automatically mark expired consents as inactive
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
            ViewBag.Conditions = conditions;
            ViewBag.Allergies = allergies;
            ViewBag.Medications = medications;
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
            List<int>? selectedConditionIds,
            List<int>? selectedAllergyIds,
            List<int>? selectedMedicationIds,
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

                    // Convert local browser-submitted date to UTC
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
            // Validate selected medical information belongs to patient
            // -------------------------------------------------------------

            selectedConditionIds ??= new List<int>();
            selectedAllergyIds ??= new List<int>();
            selectedMedicationIds ??= new List<int>();


            var validConditionIds = await _context.PatientConditions
                .Where(pc =>
                    pc.PatientID == patient.PatientID &&
                    selectedConditionIds.Contains(pc.ConditionId) &&
                    pc.Condition.IsActive)
                .Select(pc => pc.ConditionId)
                .Distinct()
                .ToListAsync();


            var validAllergyIds = await _context.PatientAllergies
                .Where(pa =>
                    pa.PatientID == patient.PatientID &&
                    selectedAllergyIds.Contains(pa.AllergyId) &&
                    pa.Allergy.IsActive)
                .Select(pa => pa.AllergyId)
                .Distinct()
                .ToListAsync();


            var validMedicationIds = await _context.PatientMedications
                .Where(pm =>
                    pm.PatientID == patient.PatientID &&
                    selectedMedicationIds.Contains(pm.MedicationId) &&
                    pm.Medication.IsActive)
                .Select(pm => pm.MedicationId)
                .Distinct()
                .ToListAsync();


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
            // Save selected conditions
            // -------------------------------------------------------------
            foreach (var conditionId in validConditionIds)
            {
                _context.ConsentConditions.Add(
                    new ConsentCondition
                    {
                        ConsentId = consent.Id,
                        ConditionId = conditionId
                    });
            }


            // -------------------------------------------------------------
            // Save selected allergies
            // -------------------------------------------------------------
            foreach (var allergyId in validAllergyIds)
            {
                _context.ConsentAllergies.Add(
                    new ConsentAllergy
                    {
                        ConsentId = consent.Id,
                        AllergyId = allergyId
                    });
            }


            // -------------------------------------------------------------
            // Save selected medications
            // -------------------------------------------------------------
            foreach (var medicationId in validMedicationIds)
            {
                _context.ConsentMedications.Add(
                    new ConsentMedication
                    {
                        ConsentId = consent.Id,
                        MedicationId = medicationId
                    });
            }


            await _context.SaveChangesAsync();


            // -------------------------------------------------------------
            // Get shared condition names
            // -------------------------------------------------------------
            var sharedConditions = await _context.ConsentConditions
                .Where(cc => cc.ConsentId == consent.Id)
                .Include(cc => cc.Condition)
                .Select(cc => cc.Condition.Name)
                .OrderBy(name => name)
                .ToListAsync();


            // -------------------------------------------------------------
            // Get shared allergy names
            // -------------------------------------------------------------
            var sharedAllergies = await _context.ConsentAllergies
                .Where(ca => ca.ConsentId == consent.Id)
                .Include(ca => ca.Allergy)
                .Select(ca => ca.Allergy.Name)
                .OrderBy(name => name)
                .ToListAsync();


            // -------------------------------------------------------------
            // Get shared medication names
            // -------------------------------------------------------------
            var sharedMedications = await _context.ConsentMedications
                .Where(cm => cm.ConsentId == consent.Id)
                .Include(cm => cm.Medication)
                .Select(cm => cm.Medication.Name)
                .OrderBy(name => name)
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
                    sharedConditions);
            }


            TempData["Success"] =
                $"Consent granted to Dr. {doctor.Name} {doctor.Surname} " +
                $"until {expiresAt.ToLocalTime():dd MMM yyyy HH:mm}.";

            return RedirectToAction(nameof(Consent));
        }


        // ---------------------------------------------------------------------
        // Revoke Consent
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
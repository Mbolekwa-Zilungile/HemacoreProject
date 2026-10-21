using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Services;
using HemaCore.ViewModels;
using HemaCore.ViewModels.Patient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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

            // Load categories with their items (active only, ordered alphabetically)
            var conditionCategories = await _context.ConditionCategories
                .Where(c => c.IsActive)
                .Include(c => c.Conditions.Where(cond => cond.IsActive))
                .OrderBy(c => c.Name)
                .ToListAsync();

            var allergyCategories = await _context.AllergyCategories
                .Where(c => c.IsActive)
                .Include(c => c.Allergies.Where(a => a.IsActive))
                .OrderBy(c => c.Name)
                .ToListAsync();

            var medicationCategories = await _context.MedicationCategories
                .Where(c => c.IsActive)
                .Include(c => c.Medications.Where(m => m.IsActive))
                .OrderBy(c => c.Name)
                .ToListAsync();

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

                // Flattened lists for checkbox rendering
                AvailableConditions = conditionCategories
                    .SelectMany(c => c.Conditions)
                    .OrderBy(c => c.Name)
                    .ToList(),

                AvailableAllergies = allergyCategories
                    .SelectMany(c => c.Allergies)
                    .OrderBy(a => a.Name)
                    .ToList(),

                AvailableMedications = medicationCategories
                    .SelectMany(c => c.Medications)
                    .OrderBy(m => m.Name)
                    .ToList(),

                // Categories for grouping in the view
                ConditionCategories = conditionCategories,
                AllergyCategories = allergyCategories,
                MedicationCategories = medicationCategories
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
        public async Task<IActionResult> Health(string period = "all", int? testTypeId = null)
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
            {
                return RedirectToAction(nameof(CompleteProfile));
            }

            // ---------------------------------------------------------
            // DATE RANGE
            // ---------------------------------------------------------
            DateTime? fromDate = null;
            DateTime today = DateTime.Now.Date;

            switch (period?.ToLower())
            {
                case "7": fromDate = today.AddDays(-7); break;
                case "30": fromDate = today.AddDays(-30); break;
                case "90": fromDate = today.AddDays(-90); break;
                case "180": fromDate = today.AddDays(-180); break;
                case "365": fromDate = today.AddDays(-365); break;
                case "all": default: fromDate = null; period = "all"; break;
            }

            // ---------------------------------------------------------
            // LOAD AVAILABLE TESTS FOR THE DROPDOWN
            // ---------------------------------------------------------
            var availableTests = await _context.TestRequestTestTypes
                .Where(t => t.TestRequest.PatientID == patient.PatientID &&
                            t.TestResult != null &&
                            t.Status == "Verified")
                .Select(t => new
                {
                    t.TestTypeId,
                    t.TestType.TestTypeName
                })
                .Distinct()
                .OrderBy(t => t.TestTypeName)
                .ToListAsync();

            ViewBag.AvailableTests = availableTests;
            ViewBag.SelectedTestTypeId = testTypeId;

            // ---------------------------------------------------------
            // BUILD GRAPH DATA (If a test is selected)
            // ---------------------------------------------------------
            var graphLabels = new List<string>();
            var graphValues = new List<double>();
            double normalMin = 0;
            double normalMax = 0;
            string unit = "";
            bool latestIsAbnormal = false; // NEW: Tracks if the most recent test is abnormal

            if (testTypeId.HasValue)
            {
                // Get the test type details for normal ranges
                var testType = await _context.TestTypes.FindAsync(testTypeId.Value);
                if (testType != null)
                {
                    normalMin = testType.NormalRangeMin;
                    normalMax = testType.NormalRangeMax;
                    unit = testType.UnitOfMeasurement ?? "";
                }

                // Query historical results for this specific test
                var query = _context.TestRequestTestTypes
                    .Include(t => t.TestRequest)
                    .Include(t => t.TestResult)
                    .Where(t => t.TestRequest.PatientID == patient.PatientID &&
                                t.TestTypeId == testTypeId.Value &&
                                t.TestResult != null &&
                                t.Status == "Verified");

                if (fromDate.HasValue)
                {
                    query = query.Where(t => t.TestRequest.RequestDate >= fromDate.Value);
                }

                var history = await query
                    .OrderBy(t => t.TestRequest.RequestDate)
                    .ToListAsync();

                foreach (var item in history)
                {
                    if (double.TryParse(item.TestResult.ResultValue, out double val))
                    {
                        graphLabels.Add(item.TestRequest.RequestDate.ToString("dd MMM yy"));
                        graphValues.Add(val);
                    }
                }

                // Determine if the latest result is abnormal
                if (graphValues.Any())
                {
                    var latestVal = graphValues.Last();
                    latestIsAbnormal = latestVal < normalMin || latestVal > normalMax;
                }
            }

            // ---------------------------------------------------------
            // RECENT REQUESTS & CONDITIONS
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
                requestsQuery = requestsQuery.Where(r => r.RequestDate >= fromDate.Value);
            }

            var requests = await requestsQuery
                .OrderBy(r => r.RequestDate)
                .ToListAsync();

            var conditions = await _context.PatientConditions
                .Where(pc => pc.PatientID == patient.PatientID)
                .Include(pc => pc.Condition)
                .OrderBy(pc => pc.Condition.Name)
                .ToListAsync();

            // ---------------------------------------------------------
            // SEND DATA TO VIEW
            // ---------------------------------------------------------
            ViewBag.Period = period;
            ViewBag.GraphLabels = graphLabels;
            ViewBag.GraphValues = graphValues;
            ViewBag.NormalMin = normalMin;
            ViewBag.NormalMax = normalMax;
            ViewBag.Unit = unit;
            ViewBag.LatestIsAbnormal = latestIsAbnormal; // NEW: Pass to View

            ViewBag.TotalTests = requests.Sum(r => r.TestRequestTestTypes?.Count ?? 0);

            ViewBag.CompletedTests = requests
                .SelectMany(r => r.TestRequestTestTypes ?? new List<TestRequestItem>())
                .Count(t => t.Status == "Completed" || t.Status == "Verified");

            ViewBag.AbnormalResults = requests
                .SelectMany(r => r.TestRequestTestTypes ?? new List<TestRequestItem>())
                .Count(t => t.Status == "Verified" && t.TestResult != null && t.TestResult.IsAbnormal);

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

            // RELEASED test requests belonging to this patient.
            // Completed-but-not-released requests are NOT shareable because
            // the patient cannot see them yet.
            var releasedTestRequests = await _context.TestRequests
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

            // New key + backward-compatible key
            ViewBag.ReleasedTestRequests = releasedTestRequests;
            ViewBag.CompletedTestRequests = releasedTestRequests;

            ViewBag.Consents = consents;

            return View();
        }


        // ---------------------------------------------------------------------
        // Patient Consent (POST)
        //
        // The patient MAY:
        //   * grant consent with zero test selections (empty share)
        //   * grant consent and select 1..N released test requests
        //
        // The patient MUST NOT:
        //   * select requests that are not released
        //   * select requests that belong to someone else
        //   * submit ONLY invalid IDs (silently pretend to share nothing)
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

                    // datetime-local values come in as Unspecified; treat them as local.
                    var localExpiry = DateTime.SpecifyKind(
                        customExpiry.Value,
                        DateTimeKind.Local);

                    if (localExpiry.ToUniversalTime() <= now)
                    {
                        TempData["Error"] =
                            "The expiry date must be in the future.";

                        return RedirectToAction(nameof(Consent));
                    }

                    expiresAt = localExpiry.ToUniversalTime();
                    break;

                default:

                    TempData["Error"] =
                        "Please select a valid consent duration.";

                    return RedirectToAction(nameof(Consent));
            }

            // -------------------------------------------------------------
            // Validate selected test requests
            //
            // Rule:
            //  * Zero selections  -> allowed (patient grants bare consent).
            //  * Any selections   -> each must be RELEASED and belong to this patient.
            //  * Only bad IDs     -> reject (patient clearly intended to share).
            // -------------------------------------------------------------
            selectedTestRequestIds ??= new List<int>();

            var validTestRequestIds = new List<int>();

            if (selectedTestRequestIds.Any())
            {
                validTestRequestIds = await _context.TestRequests
                    .Where(r =>
                        r.PatientID == patient.PatientID &&
                        r.ReleasedToPatientDate != null &&           // <-- released only
                        selectedTestRequestIds.Contains(r.TestRequestId))
                    .Select(r => r.TestRequestId)
                    .Distinct()
                    .ToListAsync();

                if (!validTestRequestIds.Any())
                {
                    TempData["Error"] =
                        "The test results you selected are no longer available " +
                        "(they must be released to you first). " +
                        "Please refresh and try again.";

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
            // Attach any selected released test requests
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
            // Prepare notification content
            // -------------------------------------------------------------
            var sharedTestRequests = validTestRequestIds.Any()
                ? await _context.TestRequests
                    .Where(r => validTestRequestIds.Contains(r.TestRequestId))
                    .Select(r => "Test Request #" + r.TestRequestId)
                    .ToListAsync()
                : new List<string>
                  {
              "No test results were selected for sharing at this time."
                  };

            // -------------------------------------------------------------
            // Email doctor
            // -------------------------------------------------------------
            var doctorEmail = doctor.User?.Email;

            if (!string.IsNullOrWhiteSpace(doctorEmail))
            {
                try
                {
                    await _emailService.SendConsentGrantedEmailAsync(
                        doctorEmail,
                        doctor.Name,
                        patient.Name,
                        patient.Surname,
                        sharedTestRequests);
                }
                catch
                {
                    // Never block the consent flow because of email failure.
                }
            }

            TempData["Success"] =
                validTestRequestIds.Any()
                    ? $"Consent granted to Dr. {doctor.Name} {doctor.Surname} " +
                      $"until {expiresAt.ToLocalTime():dd MMM yyyy HH:mm}. " +
                      $"{validTestRequestIds.Count} test result(s) shared."
                    : $"Consent granted to Dr. {doctor.Name} {doctor.Surname} " +
                      $"until {expiresAt.ToLocalTime():dd MMM yyyy HH:mm}. " +
                      "No test results were shared yet.";

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

            // RELEASED test requests belonging to this patient.
            var releasedTestRequests = await _context.TestRequests
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

            ViewBag.ReleasedTestRequests = releasedTestRequests;
            ViewBag.CompletedTestRequests = releasedTestRequests;

            return View(requests);
        }


        // =============================================================
        // PATIENT APPROVES A DOCTOR'S CONSENT REQUEST
        //
        // Rules mirror Consent POST:
        //   * Zero selections allowed  -> approve without sharing anything.
        //   * Any selections           -> must all be released + belong to patient.
        //   * Only bad IDs             -> reject the request.
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

            // Validate selected test requests (see Consent POST rules).
            selectedTestRequestIds ??= new List<int>();

            var validTestRequestIds = new List<int>();

            if (selectedTestRequestIds.Any())
            {
                validTestRequestIds = await _context.TestRequests
                    .Where(r =>
                        r.PatientID == patient.PatientID &&
                        r.ReleasedToPatientDate != null &&        // <-- released only
                        selectedTestRequestIds.Contains(r.TestRequestId))
                    .Select(r => r.TestRequestId)
                    .Distinct()
                    .ToListAsync();

                if (!validTestRequestIds.Any())
                {
                    TempData["Error"] =
                        "The test results you selected are no longer available. " +
                        "Please refresh and try again.";

                    return RedirectToAction(nameof(ConsentRequests));
                }
            }

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
                var sharedTestRequests = validTestRequestIds.Any()
                    ? validTestRequestIds
                        .Select(id => "Test Request #" + id)
                        .ToList()
                    : new List<string>
                      {
                  "No test results were selected for sharing at this time."
                      };

                try
                {
                    await _emailService.SendConsentGrantedEmailAsync(
                        doctorEmail,
                        request.Doctor.Name,
                        patient.Name,
                        patient.Surname,
                        sharedTestRequests);
                }
                catch
                {
                    // Never block the approval flow because of email failure.
                }
            }

            TempData["Success"] =
                validTestRequestIds.Any()
                    ? $"Consent approved until {expiresAt.ToLocalTime():dd MMM yyyy HH:mm}. " +
                      $"{validTestRequestIds.Count} test result(s) shared."
                    : $"Consent approved until {expiresAt.ToLocalTime():dd MMM yyyy HH:mm}. " +
                      "No test results were shared yet.";

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

            consent.IsActive = false;
            consent.RevokedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var doctorEmail = consent.Doctor.User?.Email;

            if (!string.IsNullOrWhiteSpace(doctorEmail))
            {
                try
                {
                    await _emailService.SendConsentRevokedEmailAsync(
                        doctorEmail,
                        consent.Doctor.Name,
                        patient.Name,
                        patient.Surname);
                }
                catch
                {
                    // Never block revoke because of email failure.
                }
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
        [HttpGet]
        public async Task<IActionResult> ExportHealthReportPdf(DateTime? fromDate, DateTime? toDate)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction(nameof(CompleteProfile));

            var generatedAt = DateTime.Now;

            // Query base with includes
            var query = _context.TestRequests
                .AsNoTracking()
                .Include(r => r.Doctor)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestResult)
                        .ThenInclude(tr => tr.TestResultVerification)
                .Where(r => r.PatientID == patient.PatientID && r.ReleasedToPatientDate != null);

            // Apply Date Filters
            if (fromDate.HasValue)
                query = query.Where(r => r.ReleasedToPatientDate >= fromDate.Value);

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1);
                query = query.Where(r => r.ReleasedToPatientDate < end);
            }

            var requests = await query
                .OrderByDescending(r => r.ReleasedToPatientDate)
                .ToListAsync();

            // Formatting criteria for header
            string FormatDate(DateTime? date) => date.HasValue ? date.Value.ToString("dd MMM yyyy") : "Earliest";
            string period = $"{FormatDate(fromDate)} to {(toDate.HasValue ? FormatDate(toDate) : "Latest")}";

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken3));

                    // HEADER
                    page.Header().Column(header =>
                    {
                        header.Item().Text("HEMACORE").FontSize(18).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken3);
                        header.Item().PaddingTop(3).Text("LABORATORY INFORMATION SYSTEM").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        header.Item().PaddingTop(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Blue.Lighten2);
                        header.Item().PaddingTop(10).Text("Patient Health Summary Report").FontSize(16).Bold();
                    });

                    // CONTENT
                    page.Content().PaddingTop(15).Column(content =>
                    {
                        content.Spacing(10);

                        // PATIENT INFO
                        content.Item().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Background(QuestPDF.Helpers.Colors.Grey.Lighten4).Padding(10).Column(info =>
                        {
                            info.Spacing(3);
                            void Row(string label, string value)
                            {
                                info.Item().Row(r =>
                                {
                                    r.ConstantItem(110).Text(label).SemiBold().FontSize(9);
                                    r.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(9);
                                });
                            }
                            Row("Patient Name", $"{patient.Name} {patient.Surname}");
                            Row("Patient No.", patient.PatientID.ToString());
                            Row("ID Number", patient.IDNumber ?? "—");
                            Row("Report Period", period);
                            Row("Generated On", generatedAt.ToString("dd MMM yyyy HH:mm"));
                        });

                        // SUMMARY
                        var totalTests = requests.SelectMany(r => r.TestRequestTestTypes).Count();
                        var abnormalCount = requests.SelectMany(r => r.TestRequestTestTypes).Count(i => i.TestResult != null && i.TestResult.IsAbnormal);

                        content.Item().PaddingTop(5).Text("Summary of Results").FontSize(12).Bold();
                        content.Item().Row(row =>
                        {
                            void SummaryBox(string label, string val)
                            {
                                row.RelativeItem().Border(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(8).Column(c =>
                                {
                                    c.Item().Text(label).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                                    c.Item().Text(val).FontSize(14).Bold();
                                });
                            }
                            SummaryBox("Total Requests", requests.Count.ToString());
                            SummaryBox("Total Verified Tests", totalTests.ToString());
                            SummaryBox("Abnormal Results", abnormalCount.ToString());
                        });

                        // TEST DETAILS TABLE
                        content.Item().PaddingTop(10).Text("Detailed Test History").FontSize(12).Bold();

                        if (!requests.Any())
                        {
                            content.Item().PaddingVertical(20).AlignCenter().Text("No verified and released records found for the selected period.").Italic();
                        }
                        else
                        {
                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(60);   // Date
                                    cols.RelativeColumn(2);    // Test Name
                                    cols.RelativeColumn(1);    // Result
                                    cols.RelativeColumn(1);    // Unit
                                    cols.RelativeColumn(1.2f); // Normal Range
                                    cols.ConstantColumn(50);   // Status
                                    cols.RelativeColumn(1.5f); // Notes
                                });

                                table.Header(header =>
                                {
                                    string[] headings = { "Date", "Test Name", "Result", "Unit", "Normal Range", "Status", "Notes" };
                                    foreach (var heading in headings)
                                    {
                                        header.Cell().Background(QuestPDF.Helpers.Colors.Blue.Darken2).PaddingVertical(6).PaddingHorizontal(4)
                                              .Text(heading).FontSize(8).Bold().FontColor(QuestPDF.Helpers.Colors.White);
                                    }
                                });

                                foreach (var req in requests)
                                {
                                    // Only show verified tests
                                    foreach (var test in req.TestRequestTestTypes.Where(t => t.TestResult != null && t.TestResult.TestResultVerification?.Status == "Verified"))
                                    {
                                        var res = test.TestResult;
                                        var isAbnormal = res.IsAbnormal;

                                        void DataCell(string text, bool bold = false, bool red = false)
                                        {
                                            var cell = table.Cell().BorderBottom(0.5f).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(4);
                                            var t = cell.Text(string.IsNullOrWhiteSpace(text) ? "—" : text).FontSize(8);
                                            if (bold) t.Bold();
                                            if (red) t.FontColor(QuestPDF.Helpers.Colors.Red.Darken1);
                                        }

                                        DataCell(req.ReleasedToPatientDate?.ToString("dd/MM/yy") ?? "—");
                                        DataCell(test.TestType?.TestTypeName ?? "—", bold: true);
                                        DataCell(res.ResultValue, bold: true, red: isAbnormal);
                                        DataCell(test.TestType?.UnitOfMeasurement ?? "—");
                                        DataCell(test.TestType == null ? "—" : $"{test.TestType.NormalRangeMin} - {test.TestType.NormalRangeMax}");
                                        DataCell(isAbnormal ? "Abnormal" : "Normal", red: isAbnormal);
                                        DataCell(res.Notes ?? "—");
                                    }
                                }
                            });
                        }
                    });

                    // FOOTER
                    page.Footer().PaddingTop(8).BorderTop(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).Row(row =>
                    {
                        row.RelativeItem().Text($"HemaCore | Generated {generatedAt:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        row.ConstantItem(100).AlignRight().Text(text =>
                        {
                            text.Span("Page ").FontSize(8);
                            text.CurrentPageNumber().FontSize(8);
                            text.Span(" of ").FontSize(8);
                            text.TotalPages().FontSize(8);
                        });
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"HemaCore_HealthReport_{patient.PatientID}_{generatedAt:yyyyMMdd}.pdf");
        }
        #endregion

        #region Download File
        // ============================================================
        // DOWNLOAD RESULT PDF
        // Only shows verified results of requests already released
        // to this patient.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> DownloadResult(int id)
        {
            var patient = await GetCurrentPatientAsync();

            if (patient == null)
                return RedirectToAction(nameof(CompleteProfile));

            // Load the released request with every navigation we need.
            var request = await _context.TestRequests
                .AsNoTracking()
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestResult)
                        .ThenInclude(tr => tr.TestResultVerification)
                .FirstOrDefaultAsync(r =>
                    r.TestRequestId == id &&
                    r.PatientID == patient.PatientID &&
                    r.ReleasedToPatientDate != null);

            if (request == null)
            {
                TempData["Error"] =
                    "The requested results could not be found or have not been released yet.";

                return RedirectToAction(nameof(Result));
            }

            // Only verified results of a released request can be downloaded.
            var verifiedTests = request.TestRequestTestTypes
                .Where(i =>
                    i.Status == "Verified" &&
                    i.TestResult != null &&
                    i.TestResult.TestResultVerification != null &&
                    i.TestResult.TestResultVerification.Status == "Verified")
                .OrderBy(i => i.TestType?.TestTypeName ?? string.Empty)
                .ToList();

            if (!verifiedTests.Any())
            {
                TempData["Error"] =
                    "No verified results are available for download.";

                return RedirectToAction(nameof(Result));
            }

            // ---- Build the PDF ----
            var generatedAt = DateTime.Now;

            var patientFullName =
                $"{request.Patient?.Name} {request.Patient?.Surname}".Trim();

            if (string.IsNullOrWhiteSpace(patientFullName))
                patientFullName = "N/A";

            var doctorFullName = request.Doctor != null
                ? $"Dr. {request.Doctor.Name} {request.Doctor.Surname}".Trim()
                : "N/A";

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(35);

                    // FIX: "Arial" is not shipped with QuestPDF 2026.9+.
                    // "Lato" is the built-in font and always available.
                    page.DefaultTextStyle(x => x
                        .FontFamily("Lato")
                        .FontSize(9)
                        .FontColor(QuestPDF.Helpers.Colors.Grey.Darken3));

                    // HEADER
                    page.Header().Column(header =>
                    {
                        header.Item()
                            .Text("HEMACORE")
                            .FontSize(18)
                            .Bold()
                            .FontColor(QuestPDF.Helpers.Colors.Blue.Darken3);

                        header.Item()
                            .PaddingTop(3)
                            .Text("LABORATORY INFORMATION SYSTEM")
                            .FontSize(8)
                            .FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);

                        header.Item()
                            .PaddingTop(10)
                            .LineHorizontal(1)
                            .LineColor(QuestPDF.Helpers.Colors.Blue.Lighten2);

                        header.Item()
                            .PaddingTop(10)
                            .Text("Laboratory Test Results")
                            .FontSize(16)
                            .Bold();
                    });

                    // CONTENT
                    page.Content()
                        .PaddingTop(15)
                        .Column(content =>
                        {
                            content.Spacing(10);

                            // PATIENT / REQUEST INFO
                            content.Item()
                                .Border(1)
                                .BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                                .Background(QuestPDF.Helpers.Colors.Grey.Lighten4)
                                .Padding(12)
                                .Column(info =>
                                {
                                    info.Spacing(4);

                                    void Row(string label, string value)
                                    {
                                        info.Item().Row(r =>
                                        {
                                            r.ConstantItem(110)
                                                .Text(label)
                                                .SemiBold()
                                                .FontSize(9);

                                            r.RelativeItem()
                                                .Text(string.IsNullOrWhiteSpace(value)
                                                    ? "—"
                                                    : value)
                                                .FontSize(9);
                                        });
                                    }

                                    Row("Patient", patientFullName);
                                    Row("Patient No.", request.PatientID.ToString());
                                    Row("Request #", request.TestRequestId.ToString());
                                    Row("Requesting doctor", doctorFullName);
                                    Row("Requested on",
                                        request.RequestDate.ToString("dd MMM yyyy"));
                                    Row("Released on",
                                        request.ReleasedToPatientDate?
                                            .ToString("dd MMM yyyy HH:mm") ?? "—");
                                    Row("Generated on",
                                        generatedAt.ToString("dd MMM yyyy HH:mm"));

                                    if (!string.IsNullOrWhiteSpace(request.ReleaseNote))
                                    {
                                        info.Item()
                                            .PaddingTop(6)
                                            .Text("Doctor's note")
                                            .SemiBold()
                                            .FontSize(9);

                                        info.Item()
                                            .Text(request.ReleaseNote!)
                                            .FontSize(9);
                                    }
                                });

                            // RESULTS TABLE
                            content.Item()
                                .PaddingTop(6)
                                .Text("Verified Test Results")
                                .FontSize(12)
                                .Bold();

                            content.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2f);   // Test
                                    cols.RelativeColumn(1f);   // Result
                                    cols.RelativeColumn(1f);   // Unit
                                    cols.RelativeColumn(1.2f); // Normal range
                                    cols.RelativeColumn(1f);   // Flag
                                    cols.RelativeColumn(2f);   // Notes
                                    cols.RelativeColumn(1.2f); // Verified
                                });

                                table.Header(header =>
                                {
                                    string[] headings =
                                    {
                                "Test",
                                "Result",
                                "Unit",
                                "Normal range",
                                "Flag",
                                "Notes",
                                "Verified"
                                    };

                                    foreach (var heading in headings)
                                    {
                                        header.Cell()
                                            .Background(QuestPDF.Helpers.Colors.Blue.Darken2)
                                            .PaddingVertical(6)
                                            .PaddingHorizontal(4)
                                            .Text(heading)
                                            .FontSize(8)
                                            .Bold()
                                            .FontColor(QuestPDF.Helpers.Colors.White);
                                    }
                                });

                                foreach (var test in verifiedTests)
                                {
                                    var result = test.TestResult;
                                    if (result == null)
                                        continue;

                                    void DataCell(string value, bool bold = false, bool danger = false)
                                    {
                                        var cell = table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                                            .PaddingVertical(6)
                                            .PaddingHorizontal(4);

                                        var text = cell
                                            .Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                                            .FontSize(8);

                                        if (bold) text.Bold();
                                        if (danger) text.FontColor(QuestPDF.Helpers.Colors.Red.Darken1);
                                    }

                                    DataCell(test.TestType?.TestTypeName ?? "—", bold: true);
                                    DataCell(result.ResultValue ?? "—", bold: true, danger: result.IsAbnormal);
                                    DataCell(test.TestType?.UnitOfMeasurement ?? "—");
                                    DataCell(test.TestType == null
                                        ? "—"
                                        : $"{test.TestType.NormalRangeMin} – {test.TestType.NormalRangeMax}");
                                    DataCell(result.IsAbnormal ? "Abnormal" : "Normal");
                                    DataCell(result.Notes ?? "—");
                                    DataCell(
                                        result.TestResultVerification?.VerificationDateTime
                                            .ToString("dd/MM/yy HH:mm") ?? "—");
                                }
                            });

                            // FOOTER NOTE
                            content.Item()
                                .PaddingTop(12)
                                .Text(
                                    "These results have been verified by a laboratory technician " +
                                    "and released by your doctor.")
                                .FontSize(8)
                                .Italic()
                                .FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        });

                    // PAGE FOOTER
                    page.Footer()
                        .PaddingTop(8)
                        .BorderTop(1)
                        .BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2)
                        .Row(row =>
                        {
                            row.RelativeItem().Text(
                                $"HemaCore | Generated {generatedAt:dd/MM/yyyy HH:mm}")
                                .FontSize(8)
                                .FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);

                            row.ConstantItem(100)
                                .AlignRight()
                                .Text(text =>
                                {
                                    text.Span("Page ").FontSize(8);
                                    text.CurrentPageNumber().FontSize(8);
                                    text.Span(" of ").FontSize(8);
                                    text.TotalPages().FontSize(8);
                                });
                        });
                });
            });

            var pdfBytes = document.GeneratePdf();

            return File(
                pdfBytes,
                "application/pdf",
                $"HemaCore_Results_Request{request.TestRequestId}_{generatedAt:yyyyMMdd_HHmmss}.pdf");
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
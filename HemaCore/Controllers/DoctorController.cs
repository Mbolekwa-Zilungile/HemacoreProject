using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Services;
using HemaCore.ViewModels.DoctorViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HemaCore.Controllers
{
    public class DoctorController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;
        private readonly IEmailService _emailService;

        public DoctorController(
            AppDbContext context,
            UserManager<Users> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private async Task<Doctor?> GetCurrentDoctorAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return null;

            return await _context.Doctors
                .FirstOrDefaultAsync(d => d.UserId == user.Id);
        }

        private async Task LoadCreateTestRequestOptionsAsync(
            CreateTestRequestViewModel model)
        {
            model.Patients = await _context.Patients
                .AsNoTracking()
                .OrderBy(p => p.Surname)
                .Select(p => new PatientOptionViewModel
                {
                    PatientId = p.PatientID,
                    FullName = p.Name + " " + p.Surname,
                    IDNumber = p.IDNumber,
                    DateOfBirth = p.DateOfBirth,
                    PhoneNumber = p.PhoneNumber,
                    Address = p.StreetAddress + ", " +
                              p.Suburb + ", " +
                              p.City + ", " +
                              p.Province + ", " +
                              p.PostalCode
                })
                .ToListAsync();

            var doctor = await GetCurrentDoctorAsync();

            if (doctor != null)
            {
                var consentedUserIds =
                    await GetMyConsentedUserIdsAsync(doctor.DoctorId);

                var consentedPatientIds = await _context.Patients
                    .AsNoTracking()
                    .Where(p => consentedUserIds.Contains(p.UserId))
                    .Select(p => p.PatientID)
                    .ToListAsync();

                var conditions = await _context.PatientConditions
                    .AsNoTracking()
                    .Where(pc => consentedPatientIds.Contains(pc.PatientID))
                    .Include(pc => pc.Condition)
                    .Select(pc => new
                    {
                        pc.PatientID,
                        Name = pc.Condition.Name
                    })
                    .ToListAsync();

                var allergies = await _context.PatientAllergies
                    .AsNoTracking()
                    .Where(pa => consentedPatientIds.Contains(pa.PatientID))
                    .Include(pa => pa.Allergy)
                    .Select(pa => new
                    {
                        pa.PatientID,
                        Name = pa.Allergy.Name
                    })
                    .ToListAsync();

                var medications = await _context.PatientMedications
                    .AsNoTracking()
                    .Where(pm => consentedPatientIds.Contains(pm.PatientID))
                    .Include(pm => pm.Medication)
                    .Select(pm => new
                    {
                        pm.PatientID,
                        Name = pm.Medication.Name
                    })
                    .ToListAsync();

                foreach (var patient in model.Patients)
                {
                    if (!consentedPatientIds.Contains(patient.PatientId))
                        continue;

                    patient.Conditions = conditions
                        .Where(c => c.PatientID == patient.PatientId)
                        .Select(c => c.Name)
                        .ToList();

                    patient.Allergies = allergies
                        .Where(a => a.PatientID == patient.PatientId)
                        .Select(a => a.Name)
                        .ToList();

                    patient.Medications = medications
                        .Where(m => m.PatientID == patient.PatientId)
                        .Select(m => m.Name)
                        .ToList();
                }
            }

            model.TestTypes = await _context.TestTypes
                .Where(t => t.IsActive)
                .Include(t => t.SampleType)
                .OrderBy(t => t.TestTypeName)
                .Select(t => new TestTypeOptionViewModel
                {
                    TestTypeId = t.TestTypeId,
                    TestTypeName = t.TestTypeName ?? "",
                    SampleTypeId = t.SampleTypeId,
                    SampleTypeName = t.SampleType != null
                        ? t.SampleType.SampleName
                        : ""
                })
                .ToListAsync();

            model.SampleTypes = await _context.SampleTypes
                .Where(s => s.IsActive)
                .OrderBy(s => s.SampleName)
                .Select(s => new SampleTypeOptionViewModel
                {
                    SampleTypeId = s.SampleTypeId,
                    SampleName = s.SampleName
                })
                .ToListAsync();
        }

        private async Task LoadHistoryOptionsAsync(
            List<CheckOptionViewModel> conditionsTarget,
            List<CheckOptionViewModel> allergiesTarget,
            List<CheckOptionViewModel> medicationsTarget,
            List<int>? selectedConditionIds = null,
            List<int>? selectedAllergyIds = null,
            List<int>? selectedMedicationIds = null)
        {
            var conditions = await _context.Conditions
                .AsNoTracking()
                .Include(c => c.Category)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var allergies = await _context.Allergies
                .AsNoTracking()
                .Include(a => a.Category)
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .ToListAsync();

            var medications = await _context.Medications
                .AsNoTracking()
                .Include(m => m.Category)
                .Where(m => m.IsActive)
                .OrderBy(m => m.Name)
                .ToListAsync();

            conditionsTarget.AddRange(
                conditions.Select(c => new CheckOptionViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    CategoryId = c.CategoryId,
                    CategoryName = c.Category.Name,
                    IsSelected = selectedConditionIds?.Contains(c.Id) == true
                }));

            allergiesTarget.AddRange(
                allergies.Select(a => new CheckOptionViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    CategoryId = a.CategoryId,
                    CategoryName = a.Category.Name,
                    IsSelected = selectedAllergyIds?.Contains(a.Id) == true
                }));

            medicationsTarget.AddRange(
                medications.Select(m => new CheckOptionViewModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    CategoryId = m.CategoryId,
                    CategoryName = m.Category.Name,
                    IsSelected = selectedMedicationIds?.Contains(m.Id) == true
                }));
        }

        private async Task<List<string>> GetMyConsentedUserIdsAsync(int doctorId)
        {
            var now = DateTime.UtcNow;

            return await _context.Consents
                .Where(c =>
                    c.DoctorId == doctorId &&
                    c.IsActive &&
                    c.RevokedAt == null &&
                    (c.ExpiresAt == null || c.ExpiresAt > now))
                .Select(c => c.PatientID)
                .Distinct()
                .ToListAsync();
        }

        private async Task<bool> IsPatientConsentedAsync(
            int doctorId,
            string patientUserId)
        {
            var now = DateTime.UtcNow;

            return await _context.Consents.AnyAsync(c =>
                c.DoctorId == doctorId &&
                c.PatientID == patientUserId &&
                c.IsActive &&
                c.RevokedAt == null &&
                (c.ExpiresAt == null || c.ExpiresAt > now));
        }

        private string GeneratePatientPassword()
        {
            return "Pat@" +
                   Guid.NewGuid()
                       .ToString("N")
                       .Substring(0, 8);
        }

        // ============================================================
        // DASHBOARD
        // ============================================================

        #region Dashboard

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var requests = await _context.TestRequests
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctor.DoctorId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var model = new DashboardViewModel
            {
                DoctorName = doctor.Name + " " + doctor.Surname,

                TotalRequests = requests.Count,

                CompletedRequests = requests.Count(
                    r => r.Status == "Completed"),

                PendingRequests = requests.Count(
                    r => r.Status == "In progress" ||
                         r.Status == "Sample(s) received"),

                UrgentRequests = requests.Count(
                    r => r.Urgency == "Urgent" ||
                         r.Urgency == "STAT"),

                RecentRequests = requests
                    .Take(10)
                    .Select(r => new RecentRequestViewModel
                    {
                        TestRequestId = r.TestRequestId,
                        RequestDate = r.RequestDate,
                        PatientName = r.Patient.Name + " " + r.Patient.Surname,
                        Urgency = r.Urgency,
                        Status = r.Status
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MyRequests(string? status = null)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var query = _context.TestRequests
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctor.DoctorId);

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "InProgress")
                {
                    query = query.Where(r =>
                        r.Status == "In progress" ||
                        r.Status == "Sample(s) received");
                }
                else if (status == "Urgent")
                {
                    query = query.Where(r =>
                        r.Urgency == "Urgent" ||
                        r.Urgency == "STAT");
                }
                else
                {
                    query = query.Where(r => r.Status == status);
                }
            }

            var requests = await query
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var model = new MyRequestsViewModel
            {
                StatusFilter = status,

                Requests = requests
                    .Select(r => new RecentRequestViewModel
                    {
                        TestRequestId = r.TestRequestId,
                        RequestDate = r.RequestDate,
                        PatientName = r.Patient.Name + " " + r.Patient.Surname,
                        Urgency = r.Urgency,
                        Status = r.Status
                    })
                    .ToList()
            };

            return View(model);
        }

        #endregion

        // ============================================================
        // CREATE TEST REQUEST
        // ============================================================

        #region Create Test Request

        [HttpGet]
        public async Task<IActionResult> CreateTestRequest(int? patientId = null)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var model = new CreateTestRequestViewModel();

            await LoadCreateTestRequestOptionsAsync(model);

            if (patientId.HasValue)
            {
                var patient = await _context.Patients
                    .FirstOrDefaultAsync(p => p.PatientID == patientId.Value);

                if (patient == null)
                {
                    TempData["Error"] = "The selected patient could not be found.";
                    return RedirectToAction(nameof(ManagePatients));
                }

                if (!await IsPatientConsentedAsync(doctor.DoctorId, patient.UserId))
                {
                    TempData["Error"] =
                        "You do not have active consent for this patient. Please request consent before creating a test request.";

                    return RedirectToAction(nameof(ManagePatients));
                }

                model.PatientId = patient.PatientID;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTestRequest(
            CreateTestRequestViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PatientID == model.PatientId);

            if (patient == null)
            {
                TempData["Error"] = "The selected patient could not be found.";
                return RedirectToAction(nameof(ManagePatients));
            }

            if (!await IsPatientConsentedAsync(doctor.DoctorId, patient.UserId))
            {
                TempData["Error"] =
                    "You do not have active consent for this patient. Please request consent before creating a test request.";

                return RedirectToAction(nameof(ManagePatients));
            }

            if (model.SelectedTestTypeIds == null ||
                !model.SelectedTestTypeIds.Any())
            {
                ModelState.AddModelError(
                    "",
                    "Please select at least one test type.");
            }

            if (model.BarcodeNumbers == null ||
                !model.BarcodeNumbers.Any(b => !string.IsNullOrWhiteSpace(b)))
            {
                ModelState.AddModelError(
                    "",
                    "Please record at least one sample barcode.");
            }

            if (!ModelState.IsValid)
            {
                await LoadCreateTestRequestOptionsAsync(model);
                return View(model);
            }

            var newRequest = new TestRequest
            {
                PatientID = model.PatientId,
                DoctorId = doctor.DoctorId,
                RequestDate = DateTime.Now,
                Urgency = model.Urgency,
                ClinicalNotes = model.ClinicalNotes ?? "",
                Status = "Submitted"
            };

            foreach (var testTypeId in model.SelectedTestTypeIds)
            {
                newRequest.TestRequestTestTypes.Add(
                    new TestRequestItem
                    {
                        TestTypeId = testTypeId,
                        Status = "Submitted"
                    });
            }

            for (int i = 0; i < model.BarcodeNumbers.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(model.BarcodeNumbers[i]))
                    continue;

                int sampleTypeId =
                    model.SampleTypeIds != null &&
                    i < model.SampleTypeIds.Count &&
                    model.SampleTypeIds[i].HasValue
                        ? model.SampleTypeIds[i]!.Value
                        : 0;

                if (sampleTypeId == 0)
                {
                    ModelState.AddModelError(
                        "",
                        "Please select a sample type for every sample you have added.");

                    continue;
                }

                newRequest.Samples.Add(
                    new Sample
                    {
                        Barcode = model.BarcodeNumbers[i],
                        SampleTypeId = sampleTypeId
                    });
            }

            if (!ModelState.IsValid)
            {
                await LoadCreateTestRequestOptionsAsync(model);
                return View(model);
            }

            _context.TestRequests.Add(newRequest);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Test request submitted successfully.";

            return RedirectToAction(
                nameof(RequestStatus),
                new { id = newRequest.TestRequestId });
        }

        #endregion

        // ============================================================
        // RESULTS  ← UPDATED REGION
        // ============================================================

        #region Results

        [HttpGet]
        public async Task<IActionResult> Results(int id)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var request = await _context.TestRequests
                .Include(r => r.Patient)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestResult)
                        .ThenInclude(tr => tr.TestResultVerification)
                .FirstOrDefaultAsync(r =>
                    r.TestRequestId == id &&
                    r.DoctorId == doctor.DoctorId);

            if (request == null)
                return NotFound();

            // A result is verified only when BOTH the test item status
            // and the verification record say "Verified".
            bool IsVerified(TestRequestItem i) =>
                i.Status == "Verified" &&
                i.TestResult != null &&
                i.TestResult.TestResultVerification != null &&
                i.TestResult.TestResultVerification.Status == "Verified";

            var verifiedItems = request.TestRequestTestTypes
                .Where(IsVerified)
                .OrderBy(i => i.TestType.TestTypeName)
                .ToList();

            var pendingItems = request.TestRequestTestTypes
                .Where(i => !IsVerified(i))
                .ToList();

            var model = new ResultsViewModel
            {
                TestRequestId = request.TestRequestId,

                PatientName =
                    request.Patient.Name + " " +
                    request.Patient.Surname,

                Status = request.Status,

                // ---- New release state ----
                RequestDate = request.RequestDate,

                IsReleased = request.ReleasedToPatientDate != null,
                ReleasedToPatientDate = request.ReleasedToPatientDate,
                ReleaseNote = request.ReleaseNote,

                VerifiedCount = verifiedItems.Count,
                PendingCount = pendingItems.Count,

                // ---- Verified results only ----
                TestResults = verifiedItems
                    .Select(i => new VerifiedTestResultViewModel
                    {
                        TestTypeName =
                            i.TestType.TestTypeName ?? "",

                        UnitOfMeasurement =
                            i.TestType.UnitOfMeasurement ?? "",

                        NormalRangeMin =
                            i.TestType.NormalRangeMin,

                        NormalRangeMax =
                            i.TestType.NormalRangeMax,

                        ResultValue =
                            i.TestResult!.ResultValue,

                        IsAbnormal =
                            i.TestResult.IsAbnormal,

                        Notes =
                            i.TestResult.Notes,

                        TestRequestTestTypeId =
                            i.TestRequestTestTypeId,

                        VerifiedOn =
                            i.TestResult.TestResultVerification?
                                .VerificationDateTime
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleaseResults(
            int requestId,
            string? releaseNote)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var request = await _context.TestRequests
                .Include(r => r.Patient)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestResult)
                        .ThenInclude(tr => tr.TestResultVerification)
                .FirstOrDefaultAsync(r =>
                    r.TestRequestId == requestId &&
                    r.DoctorId == doctor.DoctorId);

            if (request == null)
                return NotFound();

            // Guard 1: already released.
            if (request.ReleasedToPatientDate != null)
            {
                TempData["Error"] =
                    "These results have already been released to the patient.";

                return RedirectToAction(nameof(Results),
                    new { id = requestId });
            }

            // Guard 2: at least one verified result.
            bool IsVerified(TestRequestItem i) =>
                i.Status == "Verified" &&
                i.TestResult != null &&
                i.TestResult.TestResultVerification != null &&
                i.TestResult.TestResultVerification.Status == "Verified";

            var verifiedItems = request.TestRequestTestTypes
                .Where(IsVerified)
                .ToList();

            if (!verifiedItems.Any())
            {
                TempData["Error"] =
                    "You cannot release results until at least one test " +
                    "has been verified by a lab technician.";

                return RedirectToAction(nameof(Results),
                    new { id = requestId });
            }

            // Guard 3: cancelled requests cannot be released.
            if (request.Status == "Cancelled")
            {
                TempData["Error"] =
                    "Cancelled requests cannot have their results released.";

                return RedirectToAction(nameof(Results),
                    new { id = requestId });
            }

            // ---- Release ----
            var now = DateTime.Now;

            request.ReleasedToPatientDate = now;
            request.ReleaseNote = releaseNote?.Trim();

            // Promote the request status only when EVERY test is verified.
            var allTestsVerified = request.TestRequestTestTypes.All(IsVerified);

            if (allTestsVerified)
                request.Status = "Released by doctor";

            await _context.SaveChangesAsync();

            // ---- Notify the patient (best-effort, never blocks release) ----
            try
            {
                var patientUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.Patient.UserId);

                var patientEmail = patientUser?.Email;

                if (!string.IsNullOrWhiteSpace(patientEmail) &&
                    _emailService != null)
                {
                    // If you do not have this method on IEmailService yet,
                    // delete this block — the release still works fine.
                    await _emailService.SendResultsReleasedEmailAsync(
                        patientEmail,
                        request.Patient.Name,
                        request.Patient.Surname,
                        request.TestRequestId,
                        verifiedItems.Count);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Results released, but patient email failed: {ex.Message}");
            }

            TempData["Success"] =
                $"Results for Request #{request.TestRequestId} have been " +
                $"released to the patient ({verifiedItems.Count} result(s)).";

            return RedirectToAction(nameof(Results),
                new { id = requestId });
        }

        #endregion

        // ============================================================
        // ALERTS
        // ============================================================

        #region Alerts

        [HttpGet]
        public async Task<IActionResult> Alerts(int days = 5)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            if (days <= 0)
                days = 5;

            var cutoffDate =
                DateTime.Now.AddDays(-days);

            var alerts = await _context.Alerts
                .Where(a =>
                    a.DoctorID == doctor.DoctorId &&
                    a.CreatedDateTime >= cutoffDate)
                .OrderByDescending(a => a.CreatedDateTime)
                .ToListAsync();

            var model = new AlertsViewModel
            {
                SelectedDays = days,

                UnreadCount =
                    alerts.Count(a => !a.IsRead),

                Alerts = alerts
                    .Select(a => new AlertItemViewModel
                    {
                        AlertId = a.AlertId,
                        PatientName = a.PatientName,
                        TestName = a.TestName,
                        ResultValue = a.ResultValue,
                        NormalRangeMin = a.NormalRangeMin,
                        NormalRangeMax = a.NormalRangeMax,
                        UnitOfMeasurement = a.UnitOfMeasurement,
                        Message = a.Message,
                        IsRead = a.IsRead,
                        CreatedDateTime = a.CreatedDateTime
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAlertAsRead(int alertId)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var alert = await _context.Alerts
                .FirstOrDefaultAsync(a =>
                    a.AlertId == alertId &&
                    a.DoctorID == doctor.DoctorId);

            if (alert != null)
            {
                alert.IsRead = true;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Alerts));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAlertsAsRead()
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var unread = await _context.Alerts
                .Where(a =>
                    a.DoctorID == doctor.DoctorId &&
                    !a.IsRead)
                .ToListAsync();

            foreach (var alert in unread)
            {
                alert.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Alerts));
        }

        #endregion

        // ============================================================
        // MANAGE PATIENTS
        // ============================================================

        #region Manage Patients

        [HttpGet]
        public async Task<IActionResult> ManagePatients()
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var patients = await _context.Patients
                .AsNoTracking()
                .OrderBy(p => p.Surname)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var patientIds = patients
                .Select(p => p.PatientID)
                .ToList();

            var userIds = patients
                .Select(p => p.UserId)
                .Distinct()
                .ToList();

            var emails = await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email);

            var requestCounts = await _context.TestRequests
                .AsNoTracking()
                .Where(r =>
                    r.DoctorId == doctor.DoctorId &&
                    patientIds.Contains(r.PatientID))
                .GroupBy(r => r.PatientID)
                .Select(g => new
                {
                    PatientId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.PatientId, x => x.Count);

            var now = DateTime.UtcNow;

            var consentedUserIds = await _context.Consents
                .AsNoTracking()
                .Where(c =>
                    c.DoctorId == doctor.DoctorId &&
                    c.IsActive &&
                    c.RevokedAt == null &&
                    (c.ExpiresAt == null || c.ExpiresAt > now))
                .Select(c => c.PatientID)
                .Distinct()
                .ToListAsync();

            var consentedPatientIds = patients
                .Where(p => consentedUserIds.Contains(p.UserId))
                .Select(p => p.PatientID)
                .ToList();

            var conditions = await _context.PatientConditions
                .AsNoTracking()
                .Where(pc => consentedPatientIds.Contains(pc.PatientID))
                .Include(pc => pc.Condition)
                .ToListAsync();

            var allergies = await _context.PatientAllergies
                .AsNoTracking()
                .Where(pa => consentedPatientIds.Contains(pa.PatientID))
                .Include(pa => pa.Allergy)
                .ToListAsync();

            var medications = await _context.PatientMedications
                .AsNoTracking()
                .Where(pm => consentedPatientIds.Contains(pm.PatientID))
                .Include(pm => pm.Medication)
                .ToListAsync();

            var model = patients
                .Select(p =>
                {
                    bool hasConsent = consentedUserIds.Contains(p.UserId);

                    return new PatientListItemViewModel
                    {
                        PatientId = p.PatientID,

                        FullName = p.Name + " " + p.Surname,

                        IDNumber = p.IDNumber,

                        PhoneNumber = p.PhoneNumber,

                        Email = emails.TryGetValue(p.UserId, out var email)
                            ? email ?? ""
                            : "",

                        RequestCount = requestCounts.TryGetValue(
                            p.PatientID, out var count)
                            ? count
                            : 0,

                        Conditions = hasConsent
                            ? conditions
                                .Where(c => c.PatientID == p.PatientID)
                                .Select(c => c.Condition.Name)
                                .ToList()
                            : new List<string>(),

                        Allergies = hasConsent
                            ? allergies
                                .Where(a => a.PatientID == p.PatientID)
                                .Select(a => a.Allergy.Name)
                                .ToList()
                            : new List<string>(),

                        Medications = hasConsent
                            ? medications
                                .Where(m => m.PatientID == p.PatientID)
                                .Select(m => m.Medication.Name)
                                .ToList()
                            : new List<string>()
                    };
                })
                .ToList();

            ViewBag.ConsentedUserIds = consentedUserIds;

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SearchPatientById(string? idNumber)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(idNumber) ||
                idNumber.Length != 13 ||
                !idNumber.All(char.IsDigit))
            {
                return BadRequest(new
                {
                    found = false,
                    hasConsent = false,
                    message = "Please enter the full 13-digit ID number."
                });
            }

            var patient = await _context.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IDNumber == idNumber);

            if (patient == null)
            {
                return NotFound(new
                {
                    found = false,
                    hasConsent = false,
                    message = "No patient was found with this ID number. You can add the patient if they are not registered."
                });
            }

            var hasConsent = await IsPatientConsentedAsync(
                doctor.DoctorId,
                patient.UserId);

            if (!hasConsent)
            {
                return Ok(new
                {
                    found = true,
                    hasConsent = false,
                    patientId = patient.PatientID,
                    fullName = patient.Name + " " + patient.Surname,
                    message = "This patient is registered, but you do not have active consent. Please request consent from the patient before accessing their medical history."
                });
            }

            var conditions = await _context.PatientConditions
                .AsNoTracking()
                .Where(pc => pc.PatientID == patient.PatientID)
                .Select(pc => pc.Condition.Name)
                .ToListAsync();

            var allergies = await _context.PatientAllergies
                .AsNoTracking()
                .Where(pa => pa.PatientID == patient.PatientID)
                .Select(pa => pa.Allergy.Name)
                .ToListAsync();

            var medications = await _context.PatientMedications
                .AsNoTracking()
                .Where(pm => pm.PatientID == patient.PatientID)
                .Select(pm => pm.Medication.Name)
                .ToListAsync();

            var address = string.Join(", ", new[]
            {
                patient.StreetAddress,
                patient.Suburb,
                patient.City,
                patient.Province,
                patient.PostalCode
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

            return Ok(new
            {
                found = true,
                hasConsent = true,
                patientId = patient.PatientID,
                fullName = patient.Name + " " + patient.Surname,
                idNumber = patient.IDNumber,
                dateOfBirth = patient.DateOfBirth,
                phoneNumber = patient.PhoneNumber,
                address,
                conditions,
                allergies,
                medications,
                message = "Active consent confirmed. The patient's medical history is available."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestConsent(int patientId)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PatientID == patientId);

            if (patient == null)
            {
                TempData["Error"] = "No patient was found.";
                return RedirectToAction(nameof(ManagePatients));
            }

            if (await IsPatientConsentedAsync(doctor.DoctorId, patient.UserId))
            {
                TempData["Error"] =
                    "This patient has already granted you active consent.";

                return RedirectToAction(nameof(ManagePatients));
            }

            var pendingRequest = await _context.ConsentRequests
                .AnyAsync(r =>
                    r.PatientID == patient.UserId &&
                    r.DoctorId == doctor.DoctorId &&
                    r.Status == "Pending");

            if (pendingRequest)
            {
                TempData["Error"] =
                    "You already have a pending consent request for this patient.";

                return RedirectToAction(nameof(ManagePatients));
            }

            var request = new ConsentRequest
            {
                PatientID = patient.UserId,
                DoctorId = doctor.DoctorId,
                RequestedAt = DateTime.UtcNow,
                Status = "Pending"
            };

            _context.ConsentRequests.Add(request);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Consent request sent to the patient. They must approve it before you can access their records.";

            return RedirectToAction(nameof(ManagePatients));
        }


        [HttpGet]
        public async Task<IActionResult> PatientDetails(int id)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PatientID == id);

            if (patient == null)
            {
                TempData["Error"] = "The selected patient could not be found.";
                return RedirectToAction(nameof(ManagePatients));
            }

            if (!await IsPatientConsentedAsync(doctor.DoctorId, patient.UserId))
            {
                TempData["Error"] =
                    "You do not have active consent for this patient. Please request consent before viewing their medical history.";

                return RedirectToAction(nameof(ManagePatients));
            }

            // Load medical conditions.
            var conditions = await _context.PatientConditions
                .Where(pc => pc.PatientID == patient.PatientID)
                .Where(pc => pc.Condition != null)
                .Select(pc => pc.Condition!.Name)
                .ToListAsync();

            // Load allergies.
            var allergies = await _context.PatientAllergies
                .Where(pa => pa.PatientID == patient.PatientID)
                .Where(pa => pa.Allergy != null)
                .Select(pa => pa.Allergy!.Name)
                .ToListAsync();

            // Load current medications.
            var medications = await _context.PatientMedications
                .Where(pm => pm.PatientID == patient.PatientID)
                .Where(pm => pm.Medication != null)
                .Select(pm => pm.Medication!.Name)
                .ToListAsync();

            // Load all existing test requests for this patient.
            var requests = await _context.TestRequests
                .Where(r => r.PatientID == patient.PatientID)
                .OrderByDescending(r => r.RequestDate)
                .Select(r => new PatientRequestSummaryViewModel
                {
                    TestRequestId = r.TestRequestId,
                    RequestDate = r.RequestDate,
                    Urgency = r.Urgency,
                    Status = r.Status
                })
                .ToListAsync();

            var model = new PatientDetailsViewModel
            {
                PatientId = patient.PatientID,
                FullName = $"{patient.Name} {patient.Surname}",
                IDNumber = patient.IDNumber,
                DateOfBirth = patient.DateOfBirth,
                PhoneNumber = patient.PhoneNumber,

                // Email is stored on the existing Identity user, not Patient.
                Email = string.Empty,

                Address = string.Join(", ",
                    new[]
                    {
                patient.StreetAddress,
                patient.Suburb,
                patient.City,
                patient.Province,
                patient.PostalCode
                    }.Where(x => !string.IsNullOrWhiteSpace(x))),

                Conditions = conditions,
                Allergies = allergies,
                Medications = medications,
                Requests = requests 
            };

            return View(model);
        }


        #endregion

        // ============================================================
        // REQUEST STATUS
        // ============================================================

        #region Request Status

        [HttpGet]
        public async Task<IActionResult> RequestStatus(int id)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var request = await _context.TestRequests
                .Include(r => r.Patient)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Include(r => r.Samples)
                    .ThenInclude(s => s.SampleType)
                .FirstOrDefaultAsync(r =>
                    r.TestRequestId == id &&
                    r.DoctorId == doctor.DoctorId);

            if (request == null)
                return NotFound();

            var model = new RequestStatusViewModel
            {
                TestRequestId = request.TestRequestId,

                PatientName =
                    request.Patient.Name + " " +
                    request.Patient.Surname,

                Status = request.Status,
                RequestDate = request.RequestDate,
                Urgency = request.Urgency,
                CancelReason = request.CancelReason,
                CancellationDate = request.CancellationDate,

                TestItems = request.TestRequestTestTypes
                    .Select(i => new TestItemStatusViewModel
                    {
                        TestTypeName = i.TestType.TestTypeName ?? "",
                        Status = i.Status
                    })
                    .ToList(),

                Samples = request.Samples
                    .Select(s => new SampleStatusViewModel
                    {
                        Barcode = s.Barcode,
                        SampleTypeName = s.SampleType.SampleName
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(int id, string reason)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var request = await _context.TestRequests
                .FirstOrDefaultAsync(r =>
                    r.TestRequestId == id &&
                    r.DoctorId == doctor.DoctorId);

            if (request == null)
                return NotFound();

            if (request.Status == "Submitted" ||
                request.Status == "Sample(s) received")
            {
                request.Status = "Cancelled";
                request.CancelReason = reason;
                request.CancellationDate = DateTime.Now;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(RequestStatus),
                new { id = id });
        }

        #endregion

        // ============================================================
        // ADD PATIENT
        // ============================================================

        #region Add Patient

        [HttpGet]
        public async Task<IActionResult> AddPatient()
        {
            var model = new AddPatientViewModel();

            await LoadHistoryOptionsAsync(
                model.AllConditions,
                model.AllAllergies,
                model.AllMedications);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPatient(AddPatientViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            string email = model.Email?.Trim() ?? "";
            string idNumber = model.IDNumber?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(idNumber))
            {
                bool idExists = await _context.Patients
                    .AnyAsync(p => p.IDNumber.Trim() == idNumber);

                if (idExists)
                {
                    ModelState.AddModelError(
                        nameof(model.IDNumber),
                        "A patient with this ID number already exists. You cannot register the same patient twice.");
                }
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var existingUser = await _userManager.FindByEmailAsync(email);

                if (existingUser != null)
                {
                    ModelState.AddModelError(
                        nameof(model.Email),
                        "This email address is already registered.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.AllConditions.Clear();
                model.AllAllergies.Clear();
                model.AllMedications.Clear();

                await LoadHistoryOptionsAsync(
                    model.AllConditions,
                    model.AllAllergies,
                    model.AllMedications,
                    model.SelectedConditionIds,
                    model.SelectedAllergyIds,
                    model.SelectedMedicationIds);

                return View(model);
            }

            string temporaryPassword = GeneratePatientPassword();

            var user = new Users
            {
                FullName = $"{model.Name.Trim()} {model.Surname.Trim()}",
                UserName = email,
                Email = email,
                PhoneNumber = model.PhoneNumber.Trim(),
                EmailConfirmed = true
            };

            var userResult = await _userManager.CreateAsync(user, temporaryPassword);

            if (!userResult.Succeeded)
            {
                foreach (var error in userResult.Errors)
                    ModelState.AddModelError("", error.Description);

                model.AllConditions.Clear();
                model.AllAllergies.Clear();
                model.AllMedications.Clear();

                await LoadHistoryOptionsAsync(
                    model.AllConditions,
                    model.AllAllergies,
                    model.AllMedications,
                    model.SelectedConditionIds,
                    model.SelectedAllergyIds,
                    model.SelectedMedicationIds);

                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Patient");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError("", error.Description);

                model.AllConditions.Clear();
                model.AllAllergies.Clear();
                model.AllMedications.Clear();

                await LoadHistoryOptionsAsync(
                    model.AllConditions,
                    model.AllAllergies,
                    model.AllMedications,
                    model.SelectedConditionIds,
                    model.SelectedAllergyIds,
                    model.SelectedMedicationIds);

                return View(model);
            }

            var patient = new Patient
            {
                Name = model.Name.Trim(),
                Surname = model.Surname.Trim(),
                IDNumber = idNumber,
                DateOfBirth = model.DateOfBirth,
                PhoneNumber = model.PhoneNumber.Trim(),
                Gender = model.Gender.Trim(),
                StreetAddress = model.StreetAddress.Trim(),
                Suburb = model.Suburb.Trim(),
                City = model.City.Trim(),
                Province = model.Province.Trim(),
                PostalCode = model.PostalCode.Trim(),
                UserId = user.Id
            };

            _context.Patients.Add(patient);

            await _context.SaveChangesAsync();

            foreach (var conditionId in model.SelectedConditionIds ?? new List<int>())
            {
                _context.PatientConditions.Add(new PatientCondition
                {
                    PatientID = patient.PatientID,
                    ConditionId = conditionId
                });
            }

            foreach (var allergyId in model.SelectedAllergyIds ?? new List<int>())
            {
                _context.PatientAllergies.Add(new PatientAllergy
                {
                    PatientID = patient.PatientID,
                    AllergyId = allergyId
                });
            }

            foreach (var medicationId in model.SelectedMedicationIds ?? new List<int>())
            {
                _context.PatientMedications.Add(new PatientMedication
                {
                    PatientID = patient.PatientID,
                    MedicationId = medicationId
                });
            }

            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendWelcomeEmailAsync(
                    email,
                    model.Name.Trim(),
                    model.Surname.Trim(),
                    temporaryPassword,
                    "Patient");
            }
            catch (Exception)
            {
                Console.WriteLine(
                    "Patient created, but welcome email could not be sent.");
            }

            TempData["Success"] =
                $"Patient {model.Name} {model.Surname} was added successfully.";

            return RedirectToAction(nameof(ManagePatients));
        }

        #endregion

        // ============================================================
        // EDIT PATIENT HISTORY
        // ============================================================

        #region Edit Patient History


        [HttpGet]
        public async Task<IActionResult> EditPatientHistory(int id)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PatientID == id);

            if (patient == null)
            {
                TempData["Error"] = "The selected patient could not be found.";
                return RedirectToAction(nameof(ManagePatients));
            }

            if (!await IsPatientConsentedAsync(doctor.DoctorId, patient.UserId))
            {
                TempData["Error"] =
                    "You need active patient consent before editing medical history.";

                return RedirectToAction(nameof(ManagePatients));
            }

            var model = new EditPatientHistoryViewModel
            {
                PatientId = patient.PatientID,
                PatientName = $"{patient.Name} {patient.Surname}"
            };

            var conditions = await _context.PatientConditions
                .Where(x => x.PatientID == patient.PatientID)
                .ToListAsync();

            var allergies = await _context.PatientAllergies
                .Where(x => x.PatientID == patient.PatientID)
                .ToListAsync();

            var medications = await _context.PatientMedications
                .Where(x => x.PatientID == patient.PatientID)
                .ToListAsync();

            model.SelectedConditionIds = conditions
                .Select(x => x.ConditionId).ToList();

            model.SelectedAllergyIds = allergies
                .Select(x => x.AllergyId).ToList();

            model.SelectedMedicationIds = medications
                .Select(x => x.MedicationId).ToList();

            foreach (var item in conditions)
                model.ConditionDiagnosedDates[item.ConditionId] = item.DiagnosedDate;

            foreach (var item in allergies)
                model.AllergySeverities[item.AllergyId] = item.Severity;

            foreach (var item in medications)
            {
                model.MedicationDosages[item.MedicationId] = item.Dosage;
                model.MedicationFrequencies[item.MedicationId] = item.Frequency;
            }

            await LoadHistoryOptionsAsync(
                model.AllConditions,
                model.AllAllergies,
                model.AllMedications,
                model.SelectedConditionIds,
                model.SelectedAllergyIds,
                model.SelectedMedicationIds);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPatientHistory(
            EditPatientHistoryViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.PatientID == model.PatientId);

            if (patient == null)
            {
                TempData["Error"] = "The selected patient could not be found.";
                return RedirectToAction(nameof(ManagePatients));
            }

            if (!await IsPatientConsentedAsync(doctor.DoctorId, patient.UserId))
            {
                TempData["Error"] =
                    "You need active patient consent before editing medical history.";

                return RedirectToAction(nameof(ManagePatients));
            }

            // Only accept IDs that exist in the relevant catalogues.
            var conditionIds = (model.SelectedConditionIds ?? new List<int>())
                .Distinct()
                .Where(id => id > 0)
                .ToList();

            var allergyIds = (model.SelectedAllergyIds ?? new List<int>())
                .Distinct()
                .Where(id => id > 0)
                .ToList();

            var medicationIds = (model.SelectedMedicationIds ?? new List<int>())
                .Distinct()
                .Where(id => id > 0)
                .ToList();

            var validConditionIds = await _context.Conditions
                .Where(x => x.IsActive && conditionIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var validAllergyIds = await _context.Allergies
                .Where(x => x.IsActive && allergyIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var validMedicationIds = await _context.Medications
                .Where(x => x.IsActive && medicationIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var existingConditions = await _context.PatientConditions
                .Where(x => x.PatientID == patient.PatientID)
                .ToListAsync();

            var existingAllergies = await _context.PatientAllergies
                .Where(x => x.PatientID == patient.PatientID)
                .ToListAsync();

            var existingMedications = await _context.PatientMedications
                .Where(x => x.PatientID == patient.PatientID)
                .ToListAsync();

            _context.PatientConditions.RemoveRange(existingConditions);
            _context.PatientAllergies.RemoveRange(existingAllergies);
            _context.PatientMedications.RemoveRange(existingMedications);

            foreach (var conditionId in validConditionIds)
            {
                model.ConditionDiagnosedDates ??= new();

                model.ConditionDiagnosedDates.TryGetValue(
                    conditionId, out var diagnosedDate);

                _context.PatientConditions.Add(new PatientCondition
                {
                    PatientID = patient.PatientID,
                    ConditionId = conditionId,
                    DiagnosedDate = diagnosedDate
                });
            }

            foreach (var allergyId in validAllergyIds)
            {
                model.AllergySeverities ??= new();

                model.AllergySeverities.TryGetValue(
                    allergyId, out var severity);

                _context.PatientAllergies.Add(new PatientAllergy
                {
                    PatientID = patient.PatientID,
                    AllergyId = allergyId,
                    Severity = string.IsNullOrWhiteSpace(severity)
                        ? null
                        : severity.Trim()
                });
            }

            foreach (var medicationId in validMedicationIds)
            {
                model.MedicationDosages ??= new();
                model.MedicationFrequencies ??= new();

                model.MedicationDosages.TryGetValue(
                    medicationId, out var dosage);

                model.MedicationFrequencies.TryGetValue(
                    medicationId, out var frequency);

                _context.PatientMedications.Add(new PatientMedication
                {
                    PatientID = patient.PatientID,
                    MedicationId = medicationId,
                    Dosage = string.IsNullOrWhiteSpace(dosage)
                        ? null
                        : dosage.Trim(),
                    Frequency = string.IsNullOrWhiteSpace(frequency)
                        ? null
                        : frequency.Trim()
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Patient medical history updated successfully.";

            return RedirectToAction(
                nameof(PatientDetails),
                new { id = patient.PatientID });
        }


        #endregion

        // ============================================================
        // REPORTS
        // ============================================================

        #region Reports

        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var requests = await _context.TestRequests
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctor.DoctorId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var model = new ReportsViewModel
            {
                Requests = requests
                    .Select(r => new ReportRequestItemViewModel
                    {
                        TestRequestId = r.TestRequestId,
                        RequestDate = r.RequestDate,
                        PatientName = r.Patient.Name + " " + r.Patient.Surname,
                        Urgency = r.Urgency,
                        Status = r.Status
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reports(
            string startDate,
            string endDate)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            DateTime? start = null;
            DateTime? end = null;

            if (!string.IsNullOrWhiteSpace(startDate) &&
                DateTime.TryParse(startDate, out var parsedStart))
            {
                start = parsedStart.Date;
            }

            if (!string.IsNullOrWhiteSpace(endDate) &&
                DateTime.TryParse(endDate, out var parsedEnd))
            {
                end = parsedEnd.Date.AddDays(1);
            }

            var requests = await _context.TestRequests
                .Include(r => r.Patient)
                .Where(r =>
                    r.DoctorId == doctor.DoctorId &&
                    (start == null || r.RequestDate >= start) &&
                    (end == null || r.RequestDate < end))
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var model = new ReportsViewModel
            {
                StartDate = startDate,
                EndDate = endDate,

                Requests = requests
                    .Select(r => new ReportRequestItemViewModel
                    {
                        TestRequestId = r.TestRequestId,
                        RequestDate = r.RequestDate,
                        PatientName = r.Patient.Name + " " + r.Patient.Surname,
                        Urgency = r.Urgency,
                        Status = r.Status
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GenerateReportPdf(
            string startDate,
            string endDate)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            DateTime? start = null;
            DateTime? end = null;

            if (!string.IsNullOrWhiteSpace(startDate) &&
                DateTime.TryParse(startDate, out var parsedStart))
            {
                start = parsedStart.Date;
            }

            if (!string.IsNullOrWhiteSpace(endDate) &&
                DateTime.TryParse(endDate, out var parsedEnd))
            {
                end = parsedEnd.Date.AddDays(1);
            }

            if (start.HasValue && end.HasValue && start >= end)
            {
                return BadRequest("The start date must not be after the end date.");
            }

            var requests = await _context.TestRequests
                .AsNoTracking()
                .Include(r => r.Patient)
                .Where(r =>
                    r.DoctorId == doctor.DoctorId &&
                    (!start.HasValue || r.RequestDate >= start.Value) &&
                    (!end.HasValue || r.RequestDate < end.Value))
                .OrderByDescending(r => r.RequestDate)
                .ThenByDescending(r => r.TestRequestId)
                .ToListAsync();

            var doctorName = $"Dr. {doctor.Name} {doctor.Surname}";
            var generatedAt = DateTime.Now;

            var period = start.HasValue || end.HasValue
                ? $"{start?.ToString("dd MMM yyyy") ?? "Earliest"} - " +
                  $"{(end.HasValue ? end.Value.AddDays(-1).ToString("dd MMM yyyy") : "Latest")}"
                : "All dates";

            var completed = requests.Count(r => r.Status == "Completed");
            var inProgress = requests.Count(r => r.Status == "In Progress");
            var statRequests = requests.Count(r => r.Urgency == "STAT");
            var cancelled = requests.Count(r => r.Status == "Cancelled");

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(x => x
                        .FontFamily("Arial")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken3));

                    page.Header().Column(header =>
                    {
                        header.Item()
                            .Text("NMB-HLabSys")
                            .FontSize(11)
                            .SemiBold()
                            .FontColor(Colors.Blue.Darken2);

                        header.Item()
                            .PaddingTop(8)
                            .Text("TEST REQUESTS REPORT")
                            .FontSize(20)
                            .Bold()
                            .FontColor(Colors.Blue.Darken3);

                        header.Item()
                            .PaddingTop(5)
                            .Text("Doctor: " + doctorName)
                            .FontSize(10);

                        header.Item()
                            .Text("Report period: " + period);

                        header.Item()
                            .Text("Generated: " +
                                  generatedAt.ToString("dd MMM yyyy, HH:mm"));

                        header.Item()
                            .PaddingTop(12)
                            .LineHorizontal(1)
                            .LineColor(Colors.Blue.Lighten2);
                    });

                    page.Content()
                        .PaddingTop(18)
                        .Column(content =>
                        {
                            content.Spacing(12);

                            content.Item().Text("Report Summary")
                                .FontSize(13)
                                .Bold()
                                .FontColor(Colors.Blue.Darken3);

                            content.Item().Border(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(10)
                                .Row(row =>
                                {
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("Total Requests");
                                        col.Item().Text(requests.Count.ToString())
                                            .FontSize(16).Bold();
                                    });

                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("Completed");
                                        col.Item().Text(completed.ToString())
                                            .FontSize(16).Bold();
                                    });

                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("In Progress");
                                        col.Item().Text(inProgress.ToString())
                                            .FontSize(16).Bold();
                                    });

                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("STAT Requests");
                                        col.Item().Text(statRequests.ToString())
                                            .FontSize(16).Bold();
                                    });
                                });

                            content.Item().Text("Test Request Details")
                                .FontSize(13)
                                .Bold()
                                .FontColor(Colors.Blue.Darken3);

                            if (requests.Count == 0)
                            {
                                content.Item()
                                    .PaddingVertical(20)
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(15)
                                    .Text("No records found for the selected date range.")
                                    .FontSize(11);
                            }
                            else
                            {
                                content.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(48);
                                        columns.ConstantColumn(78);
                                        columns.RelativeColumn(2);
                                        columns.ConstantColumn(58);
                                        columns.RelativeColumn(1.2f);
                                    });

                                    table.Header(header =>
                                    {
                                        string[] headings =
                                        {
                                            "Request #",
                                            "Date",
                                            "Patient",
                                            "Urgency",
                                            "Status"
                                        };

                                        foreach (var heading in headings)
                                        {
                                            header.Cell()
                                                .Background(Colors.Blue.Darken2)
                                                .Padding(6)
                                                .Text(heading)
                                                .FontSize(8)
                                                .Bold()
                                                .FontColor(Colors.White);
                                        }
                                    });

                                    foreach (var request in requests)
                                    {
                                        var patientName = request.Patient == null
                                            ? "Unknown patient"
                                            : $"{request.Patient.Name} {request.Patient.Surname}";

                                        var rowColor = request.TestRequestId % 2 == 0
                                            ? Colors.Grey.Lighten4
                                            : Colors.White;

                                        table.Cell()
                                            .Background(rowColor)
                                            .BorderBottom(0.5f)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(6)
                                            .Text("#" + request.TestRequestId);

                                        table.Cell()
                                            .Background(rowColor)
                                            .BorderBottom(0.5f)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(6)
                                            .Text(request.RequestDate.ToString("dd MMM yyyy"));

                                        table.Cell()
                                            .Background(rowColor)
                                            .BorderBottom(0.5f)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(6)
                                            .Text(patientName);

                                        table.Cell()
                                            .Background(rowColor)
                                            .BorderBottom(0.5f)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(6)
                                            .Text(request.Urgency ?? "N/A");

                                        table.Cell()
                                            .Background(rowColor)
                                            .BorderBottom(0.5f)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(6)
                                            .Text(request.Status ?? "Unknown");
                                    }
                                });
                            }

                            content.Item()
                                .PaddingTop(8)
                                .Text($"Cancelled requests: {cancelled}")
                                .FontSize(9);
                        });

                    page.Footer().Column(footer =>
                    {
                        footer.Item()
                            .LineHorizontal(0.5f)
                            .LineColor(Colors.Grey.Lighten1);

                        footer.Item()
                            .PaddingTop(6)
                            .Row(row =>
                            {
                                row.RelativeItem().Text(
                                    $"NMB-HLabSys | Generated {generatedAt:dd MMM yyyy HH:mm}");

                                row.ConstantItem(100)
                                    .AlignRight()
                                    .Text(text =>
                                    {
                                        text.Span("Page ");
                                        text.CurrentPageNumber();
                                        text.Span(" of ");
                                        text.TotalPages();
                                    });
                            });
                    });
                });
            });

            byte[] pdf = document.GeneratePdf();

            return File(
                pdf,
                "application/pdf",
                $"TestRequestsReport_{generatedAt:yyyyMMdd_HHmm}.pdf");
        }

        #endregion

        // ============================================================
        // DOCTOR PROFILE
        // ============================================================

        #region Profile

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (doctor == null)
                return NotFound();

            var model = new DoctorProfileViewModel
            {
                DoctorId = doctor.DoctorId,
                Name = doctor.Name,
                Surname = doctor.Surname,
                HPCSANumber = doctor.HPCSANumber,
                PhoneNumber = doctor.PhoneNumber,
                Email = doctor.User?.Email ?? ""
            };

            return View("MyProfile", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePersonal(
            DoctorProfileViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.Surname))
            {
                TempData["Error"] = "Name and surname are required.";

                return RedirectToAction(nameof(Profile));
            }

            doctor.Name = model.Name.Trim();
            doctor.Surname = model.Surname.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = "Personal information updated.";

            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateContact(
            DoctorProfileViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                TempData["Error"] = "Phone number is required.";

                return RedirectToAction(nameof(Profile));
            }

            doctor.PhoneNumber = model.PhoneNumber.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = "Contact information updated.";

            return RedirectToAction(nameof(Profile));
        }

        #endregion
    }
}
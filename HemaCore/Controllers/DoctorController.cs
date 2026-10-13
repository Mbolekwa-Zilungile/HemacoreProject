using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Services;
using HemaCore.ViewModels.DoctorViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                 .OrderBy(p => p.Surname)
                 .Select(p => new PatientOptionViewModel
                 {
                     PatientId = p.PatientID,
                     FullName = p.Name + " " + p.Surname,
                     IDNumber = p.IDNumber,
                     DateOfBirth = p.DateOfBirth,
                     PhoneNumber = p.PhoneNumber,
                     Address = p.Address
                 })
                 .ToListAsync();

             model.TestTypes = await _context.TestTypes
                    .Where(t => t.IsActive)
                    .Include(t => t.SampleType)
                    .OrderBy(t => t.TestTypeName)
                    .Select(t => new TestTypeOptionViewModel
                    {
                        TestTypeId = t.TestTypeId,
                        TestTypeName = t.TestTypeName ?? "",
                        SampleTypeId = t.SampleTypeId,
                        SampleTypeName = t.SampleType != null ? t.SampleType.SampleName : ""
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
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var allergies = await _context.Allergies
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .ToListAsync();

            var medications = await _context.Medications
                .Where(m => m.IsActive)
                .OrderBy(m => m.Name)
                .ToListAsync();

            conditionsTarget.AddRange(
                conditions.Select(c => new CheckOptionViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsSelected = selectedConditionIds != null &&
                                 selectedConditionIds.Contains(c.Id)
                }));

            allergiesTarget.AddRange(
                allergies.Select(a => new CheckOptionViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    IsSelected = selectedAllergyIds != null &&
                                 selectedAllergyIds.Contains(a.Id)
                }));

            medicationsTarget.AddRange(
                medications.Select(m => new CheckOptionViewModel
                {
                    Id = m.Id,
                    Name = m.Name,
                    IsSelected = selectedMedicationIds != null &&
                                 selectedMedicationIds.Contains(m.Id)
                }));
        }

        private async Task<List<string>> GetMyConsentedUserIdsAsync(int doctorId)
        {
            return await _context.Consents
                .Where(c => c.DoctorId == doctorId && c.IsActive)
                .Select(c => c.PatientID)
                .ToListAsync();
        }

        private async Task<bool> IsPatientConsentedAsync(
            int doctorId,
            string patientUserId)
        {
            return await _context.Consents
                .AnyAsync(c =>
                    c.DoctorId == doctorId &&
                    c.PatientID == patientUserId &&
                    c.IsActive);
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
                    return NotFound();

                if (!await IsPatientConsentedAsync(
                        doctor.DoctorId,
                        patient.UserId))
                {
                    return Forbid();
                }

                model.PatientId = patientId.Value;
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
                ModelState.AddModelError(
                    nameof(model.PatientId),
                    "Please select a valid patient.");
            }
            else
            {
                if (!await IsPatientConsentedAsync(
                        doctor.DoctorId,
                        patient.UserId))
                {
                    return Forbid();
                }
            }

            if (model.SelectedTestTypeIds == null ||
                !model.SelectedTestTypeIds.Any())
            {
                ModelState.AddModelError(
                    "",
                    "Please select at least one test type.");
            }

            if (model.BarcodeNumbers == null ||
                !model.BarcodeNumbers.Any(
                    b => !string.IsNullOrWhiteSpace(b)))
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
                if (!string.IsNullOrWhiteSpace(model.BarcodeNumbers[i]))
                {
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
                            "Please select a sample type for every sample you've added.");
                        continue;
                    }

                    newRequest.Samples.Add(
                        new Sample
                        {
                            Barcode = model.BarcodeNumbers[i],
                            SampleTypeId = sampleTypeId
                        });
                }
            }
            if (!ModelState.IsValid)
            {
                await LoadCreateTestRequestOptionsAsync(model);
                return View(model);
            }
            _context.TestRequests.Add(newRequest);

            await _context.SaveChangesAsync();

            // TODO:
            // Add patient notification email when the
            // appropriate IEmailService method is available.

            TempData["Success"] =
                "Test request submitted successfully.";

            return RedirectToAction(
                nameof(RequestStatus),
                new
                {
                    id = newRequest.TestRequestId
                });
        }

        #endregion

        // ============================================================
        // RESULTS
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
                .FirstOrDefaultAsync(r =>
                    r.TestRequestId == id &&
                    r.DoctorId == doctor.DoctorId);

            if (request == null)
                return NotFound();

            var model = new ResultsViewModel
            {
                TestRequestId = request.TestRequestId,

                PatientName =
                    request.Patient.Name + " " +
                    request.Patient.Surname,

                Status = request.Status,

                TestResults = request.TestRequestTestTypes
                    .Where(i =>
                        i.Status == "Verified" &&
                        i.TestResult != null)

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
                            i.TestResult.Notes
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleaseResults(
            int requestId,
            string releaseNote)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var request = await _context.TestRequests
                .FirstOrDefaultAsync(r =>
                    r.TestRequestId == requestId &&
                    r.DoctorId == doctor.DoctorId);

            if (request == null)
                return NotFound();

            if (request.Status == "Completed")
            {
                request.Status = "Released by doctor";
                request.ReleaseNote = releaseNote;
                request.ReleasedToPatientDate = DateTime.Now;

                await _context.SaveChangesAsync();

                // TODO:
                // E-mail patient when the result notification
                // service is implemented.
            }

            return RedirectToAction(
                nameof(Results),
                new
                {
                    id = requestId
                });
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
        public async Task<IActionResult> MarkAlertAsRead(
            int alertId)
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

            var consentedUserIds =
                await GetMyConsentedUserIdsAsync(
                    doctor.DoctorId);

            var patients = await _context.Patients
                .Where(p =>
                    consentedUserIds.Contains(p.UserId))
                .OrderBy(p => p.Surname)
                .ToListAsync();

            var patientIds =
                patients.Select(p => p.PatientID).ToList();

            var userIds =
                patients.Select(p => p.UserId).ToList();

            var emails = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(
                    u => u.Id,
                    u => u.Email);

            var requestCounts = await _context.TestRequests
     .Where(r =>
         r.DoctorId == doctor.DoctorId &&
         patientIds.Contains(r.PatientID))

                 .GroupBy(r => r.PatientID)

                .Select(g => new
                {
                    PatientId = g.Key,
                    Count = g.Count()
                })

                .ToDictionaryAsync(
                    x => x.PatientId,
                    x => x.Count);

            var conditions = await _context.PatientConditions
                .Where(pc =>
                    patientIds.Contains(pc.PatientID))
                .Include(pc => pc.Condition)
                .ToListAsync();

            var allergies = await _context.PatientAllergies
                .Where(pa =>
                    patientIds.Contains(pa.PatientID))
                .Include(pa => pa.Allergy)
                .ToListAsync();

            var medications = await _context.PatientMedications
                .Where(pm =>
                    patientIds.Contains(pm.PatientID))
                .Include(pm => pm.Medication)
                .ToListAsync();

            var model = patients
                .Select(p => new PatientListItemViewModel
                {
                    PatientId = p.PatientID,

                    FullName =
                        p.Name + " " + p.Surname,

                    IDNumber = p.IDNumber,

                    PhoneNumber = p.PhoneNumber,

                    Email =
                        emails.ContainsKey(p.UserId)
                            ? emails[p.UserId]
                            : "",

                    RequestCount =
                        requestCounts.ContainsKey(
                            p.PatientID)
                            ? requestCounts[p.PatientID]
                            : 0,

                    Conditions =
                        conditions
                            .Where(c =>
                                c.PatientID ==
                                p.PatientID)
                            .Select(c =>
                                c.Condition.Name)
                            .ToList(),

                    Allergies =
                        allergies
                            .Where(a =>
                                a.PatientID ==
                                p.PatientID)
                            .Select(a =>
                                a.Allergy.Name)
                            .ToList(),

                    Medications =
                        medications
                            .Where(m =>
                                m.PatientID ==
                                p.PatientID)
                            .Select(m =>
                                m.Medication.Name)
                            .ToList()
                })
                .ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> PatientDetails(int id)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var patient = await _context.Patients
                .FirstOrDefaultAsync(
                    p => p.PatientID == id);

            if (patient == null)
                return NotFound();

            if (!await IsPatientConsentedAsync(
                    doctor.DoctorId,
                    patient.UserId))
            {
                return Forbid();
            }

            var conditions = await _context.PatientConditions
                .Where(pc =>
                    pc.PatientID == patient.PatientID)
                .Include(pc => pc.Condition)
                .Select(pc => pc.Condition.Name)
                .ToListAsync();

            var allergies = await _context.PatientAllergies
                .Where(pa =>
                    pa.PatientID == patient.PatientID)
                .Include(pa => pa.Allergy)
                .Select(pa => pa.Allergy.Name)
                .ToListAsync();

            var medications = await _context.PatientMedications
                .Where(pm =>
                    pm.PatientID == patient.PatientID)
                .Include(pm => pm.Medication)
                .Select(pm => pm.Medication.Name)
                .ToListAsync();

            var model = new PatientDetailsViewModel
            {
                PatientId = patient.PatientID,

                FullName =
                    patient.Name + " " +
                    patient.Surname,

                IDNumber = patient.IDNumber,

                DateOfBirth =
                    patient.DateOfBirth,

                PhoneNumber =
                    patient.PhoneNumber,

                Address =
                    patient.Address,

                Conditions =
                    conditions,

                Allergies =
                    allergies,

                Medications =
                    medications
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

                .Include(r =>
                    r.TestRequestTestTypes)
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
                TestRequestId =
                    request.TestRequestId,

                PatientName =
                    request.Patient.Name + " " +
                    request.Patient.Surname,

                Status =
                    request.Status,

                RequestDate =
                    request.RequestDate,

                Urgency =
                    request.Urgency,

                CancelReason =
                    request.CancelReason,

                CancellationDate =
                    request.CancellationDate,

                TestItems =
                    request.TestRequestTestTypes
                        .Select(i => new TestItemStatusViewModel
                        {
                            TestTypeName =
                                i.TestType.TestTypeName ?? "",

                            Status =
                                i.Status
                        })
                        .ToList(),

                Samples =
                    request.Samples
                        .Select(s => new SampleStatusViewModel
                        {
                            Barcode =
                                s.Barcode,

                            SampleTypeName =
                                s.SampleType.SampleName
                        })
                        .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
         
        public async Task<IActionResult> CancelRequest(
    int id,
    string reason)
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
        public async Task<IActionResult> AddPatient(
      AddPatientViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            // Validate ViewModel
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

            string email = model.Email.Trim();
            string idNumber = model.IDNumber.Trim();

            // Check duplicate email
            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email is already registered.");

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

            // Check duplicate ID number
            bool idExists = await _context.Patients
                .AnyAsync(p => p.IDNumber == idNumber);

            if (idExists)
            {
                ModelState.AddModelError(
                    nameof(model.IDNumber),
                    "This ID number is already registered.");

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

            // Generate temporary password
            string temporaryPassword = GeneratePatientPassword();

            // Create Identity user
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
                {
                    ModelState.AddModelError("", error.Description);
                }

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

            // Assign Patient role
            var roleResult = await _userManager.AddToRoleAsync(user, "Patient");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

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

            // ========================================================
            // BUILD COMBINED ADDRESS (NEW)
            // ========================================================

            var addressParts = new[]
            {
        model.StreetAddress,
        model.Suburb,
        model.City,
        model.Province,
        model.PostalCode
    }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim());

            string fullAddress = string.Join(", ", addressParts);

            // ========================================================
            // CREATE PATIENT RECORD
            // ========================================================

            var patient = new Patient
            {
                Name = model.Name.Trim(),
                Surname = model.Surname.Trim(),
                IDNumber = idNumber,
                DateOfBirth = model.DateOfBirth,
                PhoneNumber = model.PhoneNumber.Trim(),
                Gender = model.Gender.Trim(),
                Address = fullAddress,          // CHANGED
                UserId = user.Id
            };

            _context.Patients.Add(patient);

            await _context.SaveChangesAsync();

            // ========================================================
            // CREATE CONSENT
            // ========================================================

            _context.Consents.Add(
                new Consent
                {
                    PatientID = user.Id,
                    DoctorId = doctor.DoctorId,
                    IsActive = true,
                    GrantedAt = DateTime.UtcNow
                });

            // ========================================================
            // SAVE MEDICAL CONDITIONS
            // ========================================================

            foreach (var conditionId in model.SelectedConditionIds)
            {
                _context.PatientConditions.Add(
                    new PatientCondition
                    {
                        PatientID = patient.PatientID,
                        ConditionId = conditionId
                    });
            }

            // ========================================================
            // SAVE ALLERGIES
            // ========================================================

            foreach (var allergyId in model.SelectedAllergyIds)
            {
                _context.PatientAllergies.Add(
                    new PatientAllergy
                    {
                        PatientID = patient.PatientID,
                        AllergyId = allergyId
                    });
            }

            // ========================================================
            // SAVE MEDICATIONS
            // ========================================================

            foreach (var medicationId in model.SelectedMedicationIds)
            {
                _context.PatientMedications.Add(
                    new PatientMedication
                    {
                        PatientID = patient.PatientID,
                        MedicationId = medicationId
                    });
            }

            await _context.SaveChangesAsync();

            // ========================================================
            // SEND WELCOME EMAIL
            // ========================================================

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
        public async Task<IActionResult> EditPatientHistory(
            int id)
        {
            var doctor = await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            var patient = await _context.Patients
                .FirstOrDefaultAsync(
                    p => p.PatientID == id);

            if (patient == null)
                return NotFound();

            if (!await IsPatientConsentedAsync(
                    doctor.DoctorId,
                    patient.UserId))
            {
                return Forbid();
            }

            var selectedConditionIds =
                await _context.PatientConditions
                    .Where(pc =>
                        pc.PatientID ==
                        patient.PatientID)
                    .Select(pc =>
                        pc.ConditionId)
                    .ToListAsync();

            var selectedAllergyIds =
                await _context.PatientAllergies
                    .Where(pa =>
                        pa.PatientID ==
                        patient.PatientID)
                    .Select(pa =>
                        pa.AllergyId)
                    .ToListAsync();

            var selectedMedicationIds =
                await _context.PatientMedications
                    .Where(pm =>
                        pm.PatientID ==
                        patient.PatientID)
                    .Select(pm =>
                        pm.MedicationId)
                    .ToListAsync();

            var model =
                new EditPatientHistoryViewModel
                {
                    PatientId =
                        patient.PatientID,

                    PatientName =
                        patient.Name + " " +
                        patient.Surname
                };

            await LoadHistoryOptionsAsync(
                model.AllConditions,
                model.AllAllergies,
                model.AllMedications,
                selectedConditionIds,
                selectedAllergyIds,
                selectedMedicationIds);

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
                .FirstOrDefaultAsync(
                    p => p.PatientID ==
                         model.PatientId);

            if (patient == null)
                return NotFound();

            if (!await IsPatientConsentedAsync(
                    doctor.DoctorId,
                    patient.UserId))
            {
                return Forbid();
            }

            var existingConditions =
                await _context.PatientConditions
                    .Where(pc =>
                        pc.PatientID ==
                        patient.PatientID)
                    .ToListAsync();

            var existingAllergies =
                await _context.PatientAllergies
                    .Where(pa =>
                        pa.PatientID ==
                        patient.PatientID)
                    .ToListAsync();

            var existingMedications =
                await _context.PatientMedications
                    .Where(pm =>
                        pm.PatientID ==
                        patient.PatientID)
                    .ToListAsync();

            _context.PatientConditions
                .RemoveRange(existingConditions);

            _context.PatientAllergies
                .RemoveRange(existingAllergies);

            _context.PatientMedications
                .RemoveRange(existingMedications);

            foreach (var conditionId in
                     model.SelectedConditionIds)
            {
                _context.PatientConditions.Add(
                    new PatientCondition
                    {
                        PatientID =
                            patient.PatientID,

                        ConditionId =
                            conditionId
                    });
            }

            foreach (var allergyId in
                     model.SelectedAllergyIds)
            {
                _context.PatientAllergies.Add(
                    new PatientAllergy
                    {
                        PatientID =
                            patient.PatientID,

                        AllergyId =
                            allergyId
                    });
            }

            foreach (var medicationId in
                     model.SelectedMedicationIds)
            {
                _context.PatientMedications.Add(
                    new PatientMedication
                    {
                        PatientID =
                            patient.PatientID,

                        MedicationId =
                            medicationId
                    });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Patient history updated.";

            return RedirectToAction(
                nameof(PatientDetails),
                new
                {
                    id = patient.PatientID
                });
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
                .Where(r =>
                    r.DoctorId ==
                    doctor.DoctorId)
                .OrderByDescending(r =>
                    r.RequestDate)
                .ToListAsync();

            var model = new ReportsViewModel
            {
                Requests = requests
                    .Select(r =>
                        new ReportRequestItemViewModel
                        {
                            TestRequestId =
                                r.TestRequestId,

                            RequestDate =
                                r.RequestDate,

                            PatientName =
                                r.Patient.Name +
                                " " +
                                r.Patient.Surname,

                            Urgency =
                                r.Urgency,

                            Status =
                                r.Status
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
                    (start == null ||
                     r.RequestDate >= start) &&
                    (end == null ||
                     r.RequestDate < end))
                .OrderByDescending(r =>
                    r.RequestDate)
                .ToListAsync();

            var model = new ReportsViewModel
            {
                StartDate = startDate,
                EndDate = endDate,

                Requests = requests
                    .Select(r =>
                        new ReportRequestItemViewModel
                        {
                            TestRequestId =
                                r.TestRequestId,

                            RequestDate =
                                r.RequestDate,

                            PatientName =
                                r.Patient.Name +
                                " " +
                                r.Patient.Surname,

                            Urgency =
                                r.Urgency,

                            Status =
                                r.Status
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

            var requests = await _context.TestRequests
                .Include(r => r.Patient)
                .Where(r =>
                    r.DoctorId == doctor.DoctorId &&
                    (start == null ||
                     r.RequestDate >= start) &&
                    (end == null ||
                     r.RequestDate < end))
                .OrderByDescending(r =>
                    r.RequestDate)
                .ToListAsync();

            var doctorName =
                "Dr. " +
                doctor.Name + " " +
                doctor.Surname;

            var sb =
                new System.Text.StringBuilder();

            sb.Append($@"
<html>
<head>
<style>

body {{
    font-family: Arial, sans-serif;
    padding: 2rem;
    color: #333;
}}

h1 {{
    color: #1a73e8;
}}

table {{
    width: 100%;
    border-collapse: collapse;
    margin-top: 1rem;
}}

th {{
    background: #1a73e8;
    color: white;
    padding: 0.6rem 1rem;
    text-align: left;
}}

td {{
    padding: 0.5rem 1rem;
    border-bottom: 1px solid #eee;
}}

tr:nth-child(even) {{
    background: #f9f9f9;
}}

.meta {{
    color: #666;
    font-size: 0.9rem;
    margin-bottom: 1.5rem;
}}

</style>
</head>

<body>

<h1>Test Requests Report</h1>

<p class='meta'>
    Doctor: {doctorName}<br/>
    Period: {startDate ?? "All"} to {endDate ?? "All"}<br/>
    Generated: {DateTime.Now:dd MMM yyyy HH:mm}
</p>

<table>

<thead>

<tr>
    <th>Request #</th>
    <th>Date</th>
    <th>Patient</th>
    <th>Urgency</th>
    <th>Status</th>
</tr>

</thead>

<tbody>
");

            foreach (var request in requests)
            {
                var patientName =
                    request.Patient.Name +
                    " " +
                    request.Patient.Surname;

                sb.Append($@"
<tr>
    <td>#{request.TestRequestId}</td>
    <td>{request.RequestDate:dd MMM yyyy}</td>
    <td>{patientName}</td>
    <td>{request.Urgency}</td>
    <td>{request.Status}</td>
</tr>");
            }

            sb.Append(@"
</tbody>

</table>

</body>
</html>");

            ViewBag.HtmlContent =
                sb.ToString();

            return View("PrintLayout");
        }

        #endregion

        // ============================================================
        // DOCTOR PROFILE
        // ============================================================

        #region Profile

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId =
                _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(
                    d => d.UserId == userId);

            if (doctor == null)
                return NotFound();

            var model = new DoctorProfileViewModel
            {
                DoctorId =
                    doctor.DoctorId,

                Name =
                    doctor.Name,

                Surname =
                    doctor.Surname,

                HPCSANumber =
                    doctor.HPCSANumber,

                PhoneNumber =
                    doctor.PhoneNumber,

                Email =
                    doctor.User?.Email ?? ""
            };

            return View(
                "MyProfile",
                model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePersonal(
            DoctorProfileViewModel model)
        {
            var doctor =
                await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(model.Name) ||
                string.IsNullOrWhiteSpace(model.Surname))
            {
                TempData["Error"] =
                    "Name and surname are required.";

                return RedirectToAction(
                    nameof(Profile));
            }

            doctor.Name =
                model.Name.Trim();

            doctor.Surname =
                model.Surname.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Personal information updated.";

            return RedirectToAction(
                nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateContact(
            DoctorProfileViewModel model)
        {
            var doctor =
                await GetCurrentDoctorAsync();

            if (doctor == null)
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(
                    model.PhoneNumber))
            {
                TempData["Error"] =
                    "Phone number is required.";

                return RedirectToAction(
                    nameof(Profile));
            }

            doctor.PhoneNumber =
                model.PhoneNumber.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Contact information updated.";

            return RedirectToAction(
                nameof(Profile));
        }

        #endregion
    }
}
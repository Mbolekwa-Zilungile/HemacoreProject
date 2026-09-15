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

        public DoctorController(AppDbContext context, UserManager<Users> userManager, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        // ===================== HELPERS =====================

        private async Task<Doctor?> GetCurrentDoctorAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            return await _context.Doctors
                .FirstOrDefaultAsync(d => d.UserId == user.Id);
        }

        private async Task LoadCreateTestRequestOptionsAsync(CreateTestRequestViewModel model)
        {
            model.Patients = await _context.Patients
                .OrderBy(p => p.Surname)
                .Select(p => new PatientOptionViewModel
                {
                    PatientId = p.PatientID,
                    FullName = p.Name + " " + p.Surname
                })
                .ToListAsync();

            model.TestTypes = await _context.TestTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.TestTypeName)
                .Select(t => new TestTypeOptionViewModel
                {
                    TestTypeId = t.TestTypeId,
                    TestTypeName = t.TestTypeName ?? ""
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

        // ===================== DASHBOARD =====================

        #region Dashboard

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var requests = await _context.TestRequests
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctor.DoctorId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var model = new DashboardViewModel
            {
                DoctorName = doctor.Name + " " + doctor.Surname,
                TotalRequests = requests.Count,
                CompletedRequests = requests.Count(r => r.Status == "Completed"),
                PendingRequests = requests.Count(r => r.Status == "In progress" || r.Status == "Sample(s) received"),
                UrgentRequests = requests.Count(r => r.Urgency == "Urgent" || r.Urgency == "STAT"),
                RecentRequests = requests.Take(10).Select(r => new RecentRequestViewModel
                {
                    TestRequestId = r.TestRequestId,
                    RequestDate = r.RequestDate,
                    PatientName = r.Patient.Name + " " + r.Patient.Surname,
                    Urgency = r.Urgency,
                    Status = r.Status
                }).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MyRequests(string? status = null)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var query = _context.TestRequests
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctor.DoctorId);

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "InProgress")
                    query = query.Where(r => r.Status == "In progress" || r.Status == "Sample(s) received");
                else if (status == "Urgent")
                    query = query.Where(r => r.Urgency == "Urgent" || r.Urgency == "STAT");
                else
                    query = query.Where(r => r.Status == status);
            }

            var requests = await query
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var model = new MyRequestsViewModel
            {
                StatusFilter = status,
                Requests = requests.Select(r => new RecentRequestViewModel
                {
                    TestRequestId = r.TestRequestId,
                    RequestDate = r.RequestDate,
                    PatientName = r.Patient.Name + " " + r.Patient.Surname,
                    Urgency = r.Urgency,
                    Status = r.Status
                }).ToList()
            };

            return View(model);
        }

        #endregion

        // ===================== CREATE TEST REQUEST =====================

        #region Create Test Request

        [HttpGet]
        public async Task<IActionResult> CreateTestRequest(int? patientId = null)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var model = new CreateTestRequestViewModel();
            await LoadCreateTestRequestOptionsAsync(model);

            if (patientId.HasValue)
                model.PatientId = patientId.Value;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTestRequest(CreateTestRequestViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            if (model.SelectedTestTypeIds == null || !model.SelectedTestTypeIds.Any())
            {
                ModelState.AddModelError("", "Please select at least one test type.");
            }

            if (model.BarcodeNumbers == null || !model.BarcodeNumbers.Any(b => !string.IsNullOrWhiteSpace(b)))
            {
                ModelState.AddModelError("", "Please record at least one sample barcode.");
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
                newRequest.TestRequestTestTypes.Add(new TestRequestItem
                {
                    TestTypeId = testTypeId,
                    Status = "Submitted"
                });
            }

            for (int i = 0; i < model.BarcodeNumbers.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(model.BarcodeNumbers[i]))
                {
                    newRequest.Samples.Add(new Sample
                    {
                        Barcode = model.BarcodeNumbers[i],
                        SampleTypeId = model.SampleTypeIds != null && i < model.SampleTypeIds.Count
                            ? model.SampleTypeIds[i] : 0
                    });
                }
            }

            _context.TestRequests.Add(newRequest);
            await _context.SaveChangesAsync();

            // TODO: notify patient by e-mail that a test request was submitted —
            // need a method on IEmailService for this (SendWelcomeEmailAsync doesn't fit)

            TempData["Success"] = "Test request submitted successfully.";

            return RedirectToAction("RequestStatus", new { id = newRequest.TestRequestId });
        }

        #endregion
        #region Results

        [HttpGet]
        public async Task<IActionResult> Results(int id)
        {
            var request = await _context.TestRequests
                .Include(r => r.Patient)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestResult)
                .FirstOrDefaultAsync(r => r.TestRequestId == id);

            if (request == null) return NotFound();

            var model = new ResultsViewModel
            {
                TestRequestId = request.TestRequestId,
                PatientName = request.Patient.Name + " " + request.Patient.Surname,
                Status = request.Status,
                TestResults = request.TestRequestTestTypes
                    .Where(i => i.Status == "Verified" && i.TestResult != null)
                    .Select(i => new VerifiedTestResultViewModel
                    {
                        TestTypeName = i.TestType.TestTypeName ?? "",
                        UnitOfMeasurement = i.TestType.UnitOfMeasurement ?? "",
                        NormalRangeMin = i.TestType.NormalRangeMin,
                        NormalRangeMax = i.TestType.NormalRangeMax,
                        ResultValue = i.TestResult!.ResultValue,
                        IsAbnormal = i.TestResult.IsAbnormal,
                        Notes = i.TestResult.Notes
                    }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleaseResults(int requestId, string releaseNote)
        {
            var request = await _context.TestRequests.FirstOrDefaultAsync(r => r.TestRequestId == requestId);
            if (request == null) return NotFound();

            if (request.Status == "Completed")
            {
                request.Status = "Released by doctor";
                request.ReleaseNote = releaseNote;
                request.ReleasedToPatientDate = DateTime.Now;
                await _context.SaveChangesAsync();

                // TODO: e-mail patient with results PDF or appointment message
            }

            return RedirectToAction("Results", new { id = requestId });
        }

        #endregion
        #region Alerts

        [HttpGet]
        public async Task<IActionResult> Alerts(int days = 5)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var cutoffDate = DateTime.Now.AddDays(-days);

            var alerts = await _context.Alerts
                .Where(a => a.DoctorID == doctor.DoctorId && a.CreatedDateTime >= cutoffDate)
                .OrderByDescending(a => a.CreatedDateTime)
                .ToListAsync();

            var model = new AlertsViewModel
            {
                SelectedDays = days,
                UnreadCount = alerts.Count(a => !a.IsRead),
                Alerts = alerts.Select(a => new AlertItemViewModel
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
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAlertAsRead(int alertId)
        {
            var alert = await _context.Alerts.FirstOrDefaultAsync(a => a.AlertId == alertId);
            if (alert != null)
            {
                alert.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Alerts");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAlertsAsRead()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var unread = await _context.Alerts
                .Where(a => a.DoctorID == doctor.DoctorId && !a.IsRead)
                .ToListAsync();

            foreach (var alert in unread)
                alert.IsRead = true;

            await _context.SaveChangesAsync();
            return RedirectToAction("Alerts");
        }

        #endregion
        #region Manage Patients

        private async Task<List<string>> GetMyConsentedUserIdsAsync(int doctorId)
        {
            return await _context.Consents
                .Where(c => c.DoctorId == doctorId && c.IsActive)
                .Select(c => c.PatientID)
                .ToListAsync();
        }

        private async Task<bool> IsPatientConsentedAsync(int doctorId, string patientUserId)
        {
            return await _context.Consents
                .AnyAsync(c => c.DoctorId == doctorId && c.PatientID == patientUserId && c.IsActive);
        }

        [HttpGet]
        public async Task<IActionResult> ManagePatients()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var consentedUserIds = await GetMyConsentedUserIdsAsync(doctor.DoctorId);

            var patients = await _context.Patients
                .Where(p => consentedUserIds.Contains(p.UserId))
                .OrderBy(p => p.Surname)
                .Select(p => new PatientListItemViewModel
                {
                    PatientId = p.PatientID,
                    FullName = p.Name + " " + p.Surname,
                    IDNumber = p.IDNumber,
                    PhoneNumber = p.PhoneNumber
                })
                .ToListAsync();

            return View(patients);
        }

        [HttpGet]
        public async Task<IActionResult> PatientDetails(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == id);
            if (patient == null) return NotFound();

            if (!await IsPatientConsentedAsync(doctor.DoctorId, patient.UserId))
                return Forbid();

            var conditions = await _context.PatientConditions
                .Where(pc => pc.PatientID == patient.UserId)
                .Include(pc => pc.Condition)
                .Select(pc => pc.Condition.Name)
                .ToListAsync();

            var allergies = await _context.PatientAllergies
                .Where(pa => pa.PatientID == patient.UserId)
                .Include(pa => pa.Allergy)
                .Select(pa => pa.Allergy.Name)
                .ToListAsync();

            var medications = await _context.PatientMedications
                .Where(pm => pm.PatientID == patient.UserId)
                .Include(pm => pm.Medication)
                .Select(pm => pm.Medication.Name)
                .ToListAsync();

            var model = new PatientDetailsViewModel
            {
                PatientId = patient.PatientID,
                FullName = patient.Name + " " + patient.Surname,
                IDNumber = patient.IDNumber,
                DateOfBirth = patient.DateOfBirth,
                PhoneNumber = patient.PhoneNumber,
                Address = patient.Address,
                Conditions = conditions,
                Allergies = allergies,
                Medications = medications
            };

            return View(model);
        }

        #endregion
        #region Request Status

        [HttpGet]
        public async Task<IActionResult> RequestStatus(int id)
        {
            var request = await _context.TestRequests
                .Include(r => r.Patient)
                .Include(r => r.TestRequestTestTypes)
                    .ThenInclude(i => i.TestType)
                .Include(r => r.Samples)
                    .ThenInclude(s => s.SampleType)
                .FirstOrDefaultAsync(r => r.TestRequestId == id);

            if (request == null) return NotFound();

            var model = new RequestStatusViewModel
            {
                TestRequestId = request.TestRequestId,
                PatientName = request.Patient.Name + " " + request.Patient.Surname,
                Status = request.Status,
                RequestDate = request.RequestDate,
                Urgency = request.Urgency,
                CancelReason = request.CancelReason,
                CancellationDate = request.CancellationDate,
                TestItems = request.TestRequestTestTypes.Select(i => new TestItemStatusViewModel
                {
                    TestTypeName = i.TestType.TestTypeName ?? "",
                    Status = i.Status
                }).ToList(),
                Samples = request.Samples.Select(s => new SampleStatusViewModel
                {
                    Barcode = s.Barcode,
                    SampleTypeName = s.SampleType.SampleName
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(int requestId, string cancelReason)
        {
            var request = await _context.TestRequests.FirstOrDefaultAsync(r => r.TestRequestId == requestId);
            if (request == null) return NotFound();

            if (request.Status == "Submitted" || request.Status == "Sample(s) received")
            {
                request.Status = "Cancelled";
                request.CancelReason = cancelReason;
                request.CancellationDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("RequestStatus", new { id = requestId });
        }

        #endregion
        private async Task LoadHistoryOptionsAsync(
    List<CheckOptionViewModel> conditionsTarget,
    List<CheckOptionViewModel> allergiesTarget,
    List<CheckOptionViewModel> medicationsTarget,
    List<int>? selectedConditionIds = null,
    List<int>? selectedAllergyIds = null,
    List<int>? selectedMedicationIds = null)
        {
            var conditions = await _context.Conditions.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            var allergies = await _context.Allergies.Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();
            var medications = await _context.Medications.Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync();

            conditionsTarget.AddRange(conditions.Select(c => new CheckOptionViewModel
            {
                Id = c.Id,
                Name = c.Name,
                IsSelected = selectedConditionIds != null && selectedConditionIds.Contains(c.Id)
            }));

            allergiesTarget.AddRange(allergies.Select(a => new CheckOptionViewModel
            {
                Id = a.Id,
                Name = a.Name,
                IsSelected = selectedAllergyIds != null && selectedAllergyIds.Contains(a.Id)
            }));

            medicationsTarget.AddRange(medications.Select(m => new CheckOptionViewModel
            {
                Id = m.Id,
                Name = m.Name,
                IsSelected = selectedMedicationIds != null && selectedMedicationIds.Contains(m.Id)
            }));
        }

        private string GeneratePatientPassword()
        {
            return "Pat@" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
        [HttpGet]
        public async Task<IActionResult> AddPatient()
        {
            var model = new AddPatientViewModel();
            await LoadHistoryOptionsAsync(model.AllConditions, model.AllAllergies, model.AllMedications);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPatient(AddPatientViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            if (!ModelState.IsValid)
            {
                model.AllConditions.Clear(); model.AllAllergies.Clear(); model.AllMedications.Clear();
                await LoadHistoryOptionsAsync(model.AllConditions, model.AllAllergies, model.AllMedications,
                    model.SelectedConditionIds, model.SelectedAllergyIds, model.SelectedMedicationIds);
                return View(model);
            }

            string email = model.Email.Trim();
            string idNumber = model.IDNumber.Trim();

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
                model.AllConditions.Clear(); model.AllAllergies.Clear(); model.AllMedications.Clear();
                await LoadHistoryOptionsAsync(model.AllConditions, model.AllAllergies, model.AllMedications,
                    model.SelectedConditionIds, model.SelectedAllergyIds, model.SelectedMedicationIds);
                return View(model);
            }

            bool idExists = await _context.Patients.AnyAsync(p => p.IDNumber == idNumber);
            if (idExists)
            {
                ModelState.AddModelError(nameof(model.IDNumber), "This ID number is already registered.");
                model.AllConditions.Clear(); model.AllAllergies.Clear(); model.AllMedications.Clear();
                await LoadHistoryOptionsAsync(model.AllConditions, model.AllAllergies, model.AllMedications,
                    model.SelectedConditionIds, model.SelectedAllergyIds, model.SelectedMedicationIds);
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
                model.AllConditions.Clear(); model.AllAllergies.Clear(); model.AllMedications.Clear();
                await LoadHistoryOptionsAsync(model.AllConditions, model.AllAllergies, model.AllMedications,
                    model.SelectedConditionIds, model.SelectedAllergyIds, model.SelectedMedicationIds);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Patient");
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError("", error.Description);
                model.AllConditions.Clear(); model.AllAllergies.Clear(); model.AllMedications.Clear();
                await LoadHistoryOptionsAsync(model.AllConditions, model.AllAllergies, model.AllMedications,
                    model.SelectedConditionIds, model.SelectedAllergyIds, model.SelectedMedicationIds);
                return View(model);
            }

            var patient = new Patient
            {
                Name = model.Name.Trim(),
                Surname = model.Surname.Trim(),
                IDNumber = idNumber,
                DateOfBirth = model.DateOfBirth,
                PhoneNumber = model.PhoneNumber.Trim(),
                Address = model.Address ?? "",
                UserId = user.Id
            };

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            _context.Consents.Add(new Consent
            {
                PatientID = user.Id,
                DoctorId = doctor.DoctorId,
                IsActive = true,
                GrantedAt = DateTime.UtcNow
            });

            foreach (var conditionId in model.SelectedConditionIds)
                _context.PatientConditions.Add(new PatientCondition { PatientID = user.Id, ConditionId = conditionId });

            foreach (var allergyId in model.SelectedAllergyIds)
                _context.PatientAllergies.Add(new PatientAllergy { PatientID = user.Id, AllergyId = allergyId });

            foreach (var medicationId in model.SelectedMedicationIds)
                _context.PatientMedications.Add(new PatientMedication { PatientID = user.Id, MedicationId = medicationId });

            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendWelcomeEmailAsync(email, model.Name.Trim(), model.Surname.Trim(), temporaryPassword, "Patient");
            }
            catch (Exception)
            {
                Console.WriteLine("Email not sent");
            }

            TempData["Success"] = $"Patient {model.Name} {model.Surname} was added successfully.";

            return RedirectToAction(nameof(ManagePatients));
        }
        [HttpGet]
        public async Task<IActionResult> EditPatientHistory(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == id);
            if (patient == null) return NotFound();

            if (!await IsPatientConsentedAsync(doctor.DoctorId, patient.UserId))
                return Forbid();

            var selectedConditionIds = await _context.PatientConditions
                .Where(pc => pc.PatientID == patient.UserId).Select(pc => pc.ConditionId).ToListAsync();
            var selectedAllergyIds = await _context.PatientAllergies
                .Where(pa => pa.PatientID == patient.UserId).Select(pa => pa.AllergyId).ToListAsync();
            var selectedMedicationIds = await _context.PatientMedications
                .Where(pm => pm.PatientID == patient.UserId).Select(pm => pm.MedicationId).ToListAsync();

            var model = new EditPatientHistoryViewModel
            {
                PatientId = patient.PatientID,
                PatientName = patient.Name + " " + patient.Surname
            };

            await LoadHistoryOptionsAsync(model.AllConditions, model.AllAllergies, model.AllMedications,
                selectedConditionIds, selectedAllergyIds, selectedMedicationIds);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPatientHistory(EditPatientHistoryViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientID == model.PatientId);
            if (patient == null) return NotFound();

            if (!await IsPatientConsentedAsync(doctor.DoctorId, patient.UserId))
                return Forbid();

            var existingConditions = await _context.PatientConditions.Where(pc => pc.PatientID == patient.UserId).ToListAsync();
            var existingAllergies = await _context.PatientAllergies.Where(pa => pa.PatientID == patient.UserId).ToListAsync();
            var existingMedications = await _context.PatientMedications.Where(pm => pm.PatientID == patient.UserId).ToListAsync();

            _context.PatientConditions.RemoveRange(existingConditions);
            _context.PatientAllergies.RemoveRange(existingAllergies);
            _context.PatientMedications.RemoveRange(existingMedications);

            foreach (var conditionId in model.SelectedConditionIds)
                _context.PatientConditions.Add(new PatientCondition { PatientID = patient.UserId, ConditionId = conditionId });

            foreach (var allergyId in model.SelectedAllergyIds)
                _context.PatientAllergies.Add(new PatientAllergy { PatientID = patient.UserId, AllergyId = allergyId });

            foreach (var medicationId in model.SelectedMedicationIds)
                _context.PatientMedications.Add(new PatientMedication { PatientID = patient.UserId, MedicationId = medicationId });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Patient history updated.";

            return RedirectToAction(nameof(PatientDetails), new { id = patient.PatientID });
        }
        #region Reports

        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            var requests = await _context.TestRequests
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctor.DoctorId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var model = new ReportsViewModel
            {
                Requests = requests.Select(r => new ReportRequestItemViewModel
                {
                    TestRequestId = r.TestRequestId,
                    RequestDate = r.RequestDate,
                    PatientName = r.Patient.Name + " " + r.Patient.Surname,
                    Urgency = r.Urgency,
                    Status = r.Status
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Reports(string startDate, string endDate)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            DateTime? start = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? end = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate).AddDays(1);

            var requests = await _context.TestRequests
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctor.DoctorId
                    && (start == null || r.RequestDate >= start)
                    && (end == null || r.RequestDate < end))
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var model = new ReportsViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                Requests = requests.Select(r => new ReportRequestItemViewModel
                {
                    TestRequestId = r.TestRequestId,
                    RequestDate = r.RequestDate,
                    PatientName = r.Patient.Name + " " + r.Patient.Surname,
                    Urgency = r.Urgency,
                    Status = r.Status
                }).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GenerateReportPdf(string startDate, string endDate)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return Unauthorized();

            DateTime? start = string.IsNullOrEmpty(startDate) ? null : DateTime.Parse(startDate);
            DateTime? end = string.IsNullOrEmpty(endDate) ? null : DateTime.Parse(endDate).AddDays(1);

            var requests = await _context.TestRequests
                .Include(r => r.Patient)
                .Where(r => r.DoctorId == doctor.DoctorId
                    && (start == null || r.RequestDate >= start)
                    && (end == null || r.RequestDate < end))
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var doctorName = "Dr. " + doctor.Name + " " + doctor.Surname;

            var sb = new System.Text.StringBuilder();
            sb.Append($@"
        <html>
        <head>
        <style>
            body {{ font-family: Arial, sans-serif; padding: 2rem; color: #333; }}
            h1 {{ color: #1a73e8; }}
            table {{ width: 100%; border-collapse: collapse; margin-top: 1rem; }}
            th {{ background: #1a73e8; color: white; padding: 0.6rem 1rem; text-align: left; }}
            td {{ padding: 0.5rem 1rem; border-bottom: 1px solid #eee; }}
            tr:nth-child(even) {{ background: #f9f9f9; }}
            .meta {{ color: #666; font-size: 0.9rem; margin-bottom: 1.5rem; }}
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
        <tbody>");

            foreach (var r in requests)
            {
                var patientName = r.Patient.Name + " " + r.Patient.Surname;
                sb.Append($@"
            <tr>
                <td>#{r.TestRequestId}</td>
                <td>{r.RequestDate:dd MMM yyyy}</td>
                <td>{patientName}</td>
                <td>{r.Urgency}</td>
                <td>{r.Status}</td>
            </tr>");
            }

            sb.Append("</tbody></table></body></html>");

            ViewBag.HtmlContent = sb.ToString();
            return View("PrintLayout");
        }

        #endregion

    }
}
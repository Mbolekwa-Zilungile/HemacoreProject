using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Models.ViewModels;
using HemaCore.Services;
using HemaCore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HemaCore.Controllers
{
    [Authorize]
    public class LabTechnicianController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        private readonly IEmailService _emailService;

        public LabTechnicianController(
            AppDbContext context,
            UserManager<Users> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }


        // ==========================================
        // LAB TECHNICIAN DASHBOARD
        // ==========================================

        public async Task<IActionResult> Index(
            string? urgency,
            int? categoryId,
            string? dueStatus,
            int? requestNumber)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return View(new LabTechnicianDashboardViewModel
                {
                    TechnicianName = user.FullName
                });
            }

            var now = DateTime.Now;

            ViewBag.TestCategories = await _context.TestCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            var myTests = _context.TestRequestTestTypes
                .Where(t => t.LabTechnicianId == technician.LabTechnicianId);

            var samplesAwaitingReceipt = await _context.TestRequests
                .CountAsync(r =>
                    r.Status == "Submitted" &&
                    r.Samples.Any(s =>
                        s.Status != "Received" &&
                        s.Status != "Rejected" &&
                        s.Status != "Cancelled" &&
                        !s.DateTimeReceived.HasValue));

            var qualifiedTestTypeIds = await _context.TechnicianTestTypes
                .Where(tt => tt.LabTechnicianId == technician.LabTechnicianId)
                .Select(tt => tt.TestTypeId)
                .ToListAsync();

            var waitingToBeSelectedQuery = _context.TestRequestTestTypes
                .Where(t =>
                    t.LabTechnicianId == null &&
                    t.Status == "Submitted" &&
                    (t.TestRequest.Status == "Submitted" ||
                     t.TestRequest.Status == "SamplesReceived" ||
                     t.TestRequest.Status == "InProgress") &&
                    qualifiedTestTypeIds.Contains(t.TestTypeId) &&
                    _context.Samples.Any(s =>
                        s.TestRequestId == t.TestRequestId &&
                        s.SampleTypeId == t.TestType.SampleTypeId &&
                        s.DateTimeReceived != null));

            var waitingToBeSelected = await waitingToBeSelectedQuery.CountAsync();

            var filteredQuery = waitingToBeSelectedQuery;

            if (!string.IsNullOrWhiteSpace(urgency))
            {
                filteredQuery = filteredQuery.Where(t =>
                    t.TestRequest.Urgency == urgency);
            }

            if (categoryId.HasValue)
            {
                filteredQuery = filteredQuery.Where(t =>
                    t.TestType.CategoryId == categoryId.Value);
            }

            if (requestNumber.HasValue)
            {
                filteredQuery = filteredQuery.Where(t =>
                    t.TestRequestId == requestNumber.Value);
            }

            var availableTests = await filteredQuery
                .AsNoTracking()
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                    .ThenInclude(tt => tt.Category)
                .OrderBy(t => t.TestRequestId)
                .Select(t => new LabTechnicianWorkloadViewModel
                {
                    TestRequestTestTypeId = t.TestRequestTestTypeId,
                    TestRequestId = t.TestRequestId,

                    PatientName = t.TestRequest.Patient.Name + " " +
                                  t.TestRequest.Patient.Surname,

                    TestTypeName = t.TestType.TestTypeName,

                    CategoryName = t.TestType.Category != null
                        ? t.TestType.Category.CategoryName
                        : "Uncategorised",

                    Urgency = t.TestRequest.Urgency,
                    Status = t.Status,

                    DueTime = t.TestRequest.RequestDate
                        .AddHours(t.TestType.TurnAroundTime)
                })
                .ToListAsync();

            if (dueStatus == "Overdue")
            {
                availableTests = availableTests
                    .Where(t => t.DueTime.HasValue && t.DueTime.Value < now)
                    .ToList();
            }
            else if (dueStatus == "Nearing")
            {
                availableTests = availableTests
                    .Where(t =>
                        t.DueTime.HasValue &&
                        t.DueTime.Value >= now &&
                        t.DueTime.Value <= now.AddHours(2))
                    .ToList();
            }

            var workload = await myTests
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Samples)
                .Include(t => t.TestType)
                    .ThenInclude(tt => tt.Category)
                .Select(t => new LabTechnicianWorkloadViewModel
                {
                    TestRequestId = t.TestRequestId,

                    PatientName =
                        t.TestRequest.Patient.Name + " " +
                        t.TestRequest.Patient.Surname,

                    TestTypeName = t.TestType.TestTypeName,

                    CategoryName = t.TestType.Category != null
                        ? t.TestType.Category.CategoryName
                        : "Uncategorised",

                    Urgency = t.TestRequest.Urgency,

                    Status = t.Status,

                    DueTime = t.StartDateTime.HasValue
                        ? t.StartDateTime.Value.AddHours(t.TestType.TurnAroundTime)
                        : (DateTime?)null
                })
                .ToListAsync();

            var mySelectedTests = await myTests
                .CountAsync(t =>
                    t.Status == "InProgress" &&
                    t.LabTechnicianId == technician.LabTechnicianId);

            var waitingVerification = await _context.TestRequestTestTypes
                .CountAsync(t =>
                    t.Status == "Completed" &&
                    t.TestResultId != null &&
                    t.LabTechnicianId != technician.LabTechnicianId &&
                    _context.TechnicianTestTypes.Any(tt =>
                        tt.LabTechnicianId == technician.LabTechnicianId &&
                        tt.TestTypeId == t.TestTypeId));

            var waitingReview = await _context.TestResultVerifications
                .CountAsync(v =>
                    v.Status == "To Be Reviewed" &&
                    v.TestResult != null &&
                    _context.TestRequestTestTypes.Any(t =>
                        t.TestResultId == v.TestResultId &&
                        t.LabTechnicianId == technician.LabTechnicianId &&
                        t.Status == "To Be Reviewed"));

            var unfinishedStatuses = new[]
            {
                "Submitted",
                "InProgress",
                "To Be Reviewed"
            };

            var overdueTests = workload.Count(t =>
                t.DueTime.HasValue &&
                t.DueTime.Value < now &&
                unfinishedStatuses.Contains(t.Status));

            var nearingTurnaround = workload.Count(t =>
                t.DueTime.HasValue &&
                t.DueTime.Value >= now &&
                t.DueTime.Value <= now.AddHours(2) &&
                unfinishedStatuses.Contains(t.Status));

            var urgentTests = workload.Count(t =>
                t.Urgency == "STAT" &&
                unfinishedStatuses.Contains(t.Status));

            var dashboard = new LabTechnicianDashboardViewModel
            {
                TechnicianName = user.FullName,

                SamplesAwaitingReceipt = samplesAwaitingReceipt,

                TestsAwaitingProcessing = waitingToBeSelected,

                WaitingToBeSelected = waitingToBeSelected,

                MySelectedTests = mySelectedTests,

                WaitingVerification = waitingVerification,

                WaitingReview = waitingReview,

                UrgentTests = urgentTests,

                OverdueTests = overdueTests,

                NearingTurnaround = nearingTurnaround,

                Workload = workload,

                AvailableTests = availableTests,

                FilterUrgency = urgency,
                FilterCategoryId = categoryId,
                FilterDueStatus = dueStatus,
                FilterRequestNumber = requestNumber
            };

            return View(dashboard);
        }


        // ==========================================
        // RECEIVE SAMPLES
        // ==========================================

        public async Task<IActionResult> ReceiveSample()
        {
            var testRequests = await _context.TestRequests
                .Include(t => t.Patient)
                .Include(t => t.Doctor)
                .Include(t => t.Samples)
                    .ThenInclude(s => s.SampleType)
                .Include(t => t.Samples)
                    .ThenInclude(s => s.LabTechnician)
                .Where(t =>
                    t.Status == "Submitted" &&
                    t.Samples.Any(s =>
                        s.Status != "Received" &&
                        s.Status != "Rejected" &&
                        s.Status != "Cancelled" &&
                        !s.DateTimeReceived.HasValue))
                .ToListAsync();

            return View(testRequests);
        }



        [HttpGet]
        public async Task<IActionResult> ReceiveSampleDetails(int id)
        {
            var testRequest = await _context.TestRequests
                .Include(t => t.Patient)
                .Include(t => t.Doctor)
                .Include(t => t.Samples)
                    .ThenInclude(s => s.SampleType)
                .Include(t => t.Samples)
                    .ThenInclude(s => s.LabTechnician)
                .FirstOrDefaultAsync(t =>
                    t.TestRequestId == id);

            if (testRequest == null)
            {
                return NotFound();
            }

            return View(testRequest);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceiveSample(int sampleId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
                return NotFound("Lab technician profile not found.");

            var sample = await _context.Samples
                .Include(s => s.TestRequest)
                    .ThenInclude(r => r.Samples)
                .FirstOrDefaultAsync(s => s.SampleId == sampleId);

            if (sample == null)
                return NotFound("Sample not found.");

            if (sample.Status == "Rejected")
            {
                TempData["SampleError"] =
                    "This sample has been rejected and cannot be received.";

                return RedirectToAction(
                    nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            if (sample.Status == "Received" || sample.DateTimeReceived.HasValue)
            {
                TempData["SampleError"] = "This sample has already been received.";

                return RedirectToAction(
                    nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            sample.Status = "Received";
            sample.DateTimeReceived = DateTime.Now;
            sample.LabTechnicianId = technician.LabTechnicianId;

            bool allReceived = sample.TestRequest.Samples.All(s =>
                s.Status == "Received" ||
                (s.SampleId == sample.SampleId &&
                 s.DateTimeReceived.HasValue));

            if (allReceived)
            {
                sample.TestRequest.Status = "SamplesReceived";

                TempData["SampleReceivedSuccess"] =
                    $"All samples for Request #{sample.TestRequestId} have been received.";
            }
            else
            {
                TempData["SampleReceivedSuccess"] =
                    $"Sample {sample.Barcode} has been received.";
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(ReceiveSampleDetails),
                new { id = sample.TestRequestId });
        }


        // ==========================================
        // PROCESS TESTS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> ProcessTests(
            string urgency,
            int? categoryId,
            int? requestNumber,
            DateTime? dueDate)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var qualifiedTestTypeIds =
                await _context.TechnicianTestTypes
                    .Where(tt =>
                        tt.LabTechnicianId == technician.LabTechnicianId)
                    .Select(tt => tt.TestTypeId)
                    .Distinct()
                    .ToListAsync();

            var allTests = _context.TestRequestTestTypes
                .AsNoTracking();

            var submittedTests = allTests
                .Where(t => t.Status == "Submitted");

            var unassignedTests = submittedTests
                .Where(t => t.LabTechnicianId == null);

            var readyRequests = unassignedTests
                .Where(t =>
                    t.TestRequest.Status == "Submitted" ||
                    t.TestRequest.Status == "SamplesReceived" ||
                    t.TestRequest.Status == "InProgress");

            var qualifiedTests = readyRequests
                .Where(t =>
                    qualifiedTestTypeIds.Contains(t.TestTypeId));

            var receivedSampleTests = qualifiedTests
                .Where(t => _context.Samples.Any(s =>
                    s.TestRequestId == t.TestRequestId &&
                    s.SampleTypeId == t.TestType.SampleTypeId &&
                    s.DateTimeReceived.HasValue));

            ViewBag.DebugCounts = new Dictionary<string, int>
            {
                ["All test records"] = await allTests.CountAsync(),
                ["Submitted tests"] = await submittedTests.CountAsync(),
                ["Unassigned submitted tests"] = await unassignedTests.CountAsync(),
                ["Tests with eligible request status"] = await readyRequests.CountAsync(),
                ["Tests matching technician qualifications"] = await qualifiedTests.CountAsync(),
                ["Tests with required sample received"] = await receivedSampleTests.CountAsync(),
                ["Technician qualifications configured"] = qualifiedTestTypeIds.Count
            };

            var query = _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                    .ThenInclude(tt => tt.Category)
                .Where(t =>
                    t.Status == "Submitted" &&
                    t.LabTechnicianId == null &&
                    (
                        t.TestRequest.Status == "Submitted" ||
                        t.TestRequest.Status == "SamplesReceived" ||
                        t.TestRequest.Status == "InProgress"
                    ) &&
                    qualifiedTestTypeIds.Contains(t.TestTypeId) &&
                    _context.Samples.Any(s =>
                        s.TestRequestId == t.TestRequestId &&
                        s.SampleTypeId == t.TestType.SampleTypeId &&
                        s.DateTimeReceived.HasValue
                    )
                );

            if (!string.IsNullOrWhiteSpace(urgency))
            {
                query = query.Where(t =>
                    t.TestRequest.Urgency == urgency);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(t =>
                    t.TestType.CategoryId == categoryId.Value);
            }

            if (requestNumber.HasValue)
            {
                query = query.Where(t =>
                    t.TestRequestId == requestNumber.Value);
            }

            if (dueDate.HasValue)
            {
                query = query.Where(t =>
                    t.TestRequest.RequestDate.AddHours(t.TestType.TurnAroundTime)
                        .Date == dueDate.Value.Date);
            }

            var tests = await query
                .OrderBy(t => t.TestRequestId)
                .ToListAsync();

            ViewBag.Categories = await _context.TestCategories
                .Where(c => c.IsActive)
                .ToListAsync();

            return View(tests);
        }


        // ==========================================
        // TEST DETAILS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> TestDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
                return NotFound("Lab technician profile not found.");

            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                    .ThenInclude(tt => tt.Category)
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == id);

            if (test == null)
                return NotFound("Test could not be found.");

            if (test.Status != "Submitted" ||
                test.LabTechnicianId != null)
            {
                TempData["Error"] =
                    "This test has already been selected by a technician.";

                return RedirectToAction(nameof(ProcessTests));
            }

            var isAssignedTestType = await _context.TechnicianTestTypes
                .AnyAsync(tt =>
                    tt.LabTechnicianId == technician.LabTechnicianId &&
                    tt.TestTypeId == test.TestTypeId);

            if (!isAssignedTestType)
            {
                TempData["Error"] =
                    "You are not assigned to perform this test type.";

                return RedirectToAction(nameof(ProcessTests));
            }

            var requiredSampleReceived = await _context.Samples
                .AnyAsync(s =>
                    s.TestRequestId == test.TestRequestId &&
                    s.SampleTypeId == test.TestType.SampleTypeId &&
                    s.DateTimeReceived.HasValue);

            if (!requiredSampleReceived)
            {
                TempData["Error"] =
                    "The required sample has not been received.";

                return RedirectToAction(nameof(ProcessTests));
            }

            var consumables = await _context.TestTypeConsumables
                .Include(tc => tc.Consumable)
                .Where(tc => tc.TestTypeId == test.TestTypeId)
                .OrderBy(tc => tc.Consumable.ConsumableName)
                .ToListAsync();

            var now = DateTime.Now;
            var expectedFinish = now.AddHours(test.TestType.TurnAroundTime);

            ViewBag.Consumables = consumables;
            ViewBag.ExpectedFinish = expectedFinish;

            ViewBag.HasShortage = consumables.Any(tc =>
                tc.Consumable.QuantityInHand < tc.QuantityNeeded);

            return View(test);
        }


        // ==========================================
        // ADD TO MY TESTS
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToMyTests(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
                return NotFound("Lab technician profile not found.");

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                .Include(t => t.TestType)
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == id);

            if (test == null)
                return NotFound("Test could not be found.");

            if (test.Status != "Submitted" ||
                test.LabTechnicianId != null)
            {
                TempData["Error"] =
                    "This test has already been selected by a technician.";

                return RedirectToAction(nameof(ProcessTests));
            }

            if (test.TestRequest.Status != "Submitted" &&
                test.TestRequest.Status != "SamplesReceived" &&
                test.TestRequest.Status != "InProgress")
            {
                TempData["Error"] =
                    "This request is not ready for processing.";

                return RedirectToAction(nameof(ProcessTests));
            }

            var isAssignedTestType =
                await _context.TechnicianTestTypes.AnyAsync(tt =>
                    tt.LabTechnicianId == technician.LabTechnicianId &&
                    tt.TestTypeId == test.TestTypeId);

            if (!isAssignedTestType)
            {
                TempData["Error"] =
                    "You are not assigned to perform this test type.";

                return RedirectToAction(nameof(ProcessTests));
            }

            var requiredSampleReceived =
                await _context.Samples.AnyAsync(s =>
                    s.TestRequestId == test.TestRequestId &&
                    s.SampleTypeId == test.TestType.SampleTypeId &&
                    s.DateTimeReceived.HasValue);

            if (!requiredSampleReceived)
            {
                TempData["Error"] =
                    "The required sample has not been received.";

                return RedirectToAction(nameof(ProcessTests));
            }

            var consumables = await _context.TestTypeConsumables
                .Include(tc => tc.Consumable)
                .Where(tc => tc.TestTypeId == test.TestTypeId)
                .ToListAsync();

            var shortages = consumables
                .Where(tc =>
                    tc.Consumable.QuantityInHand < tc.QuantityNeeded)
                .Select(tc => tc.Consumable.ConsumableName)
                .ToList();

            if (shortages.Any())
            {
                TempData["Error"] =
                    "Not enough stock to perform this test: " +
                    string.Join(", ", shortages) + ".";

                return RedirectToAction(nameof(TestDetails), new { id });
            }

            var now = DateTime.Now;

            var claimed = await _context.TestRequestTestTypes
                .Where(t =>
                    t.TestRequestTestTypeId == id &&
                    t.Status == "Submitted" &&
                    t.LabTechnicianId == null)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(t => t.Status, "InProgress")
                    .SetProperty(
                        t => t.LabTechnicianId,
                        (int?)technician.LabTechnicianId)
                    .SetProperty(t => t.StartDateTime, now));

            if (claimed == 0)
            {
                TempData["Error"] =
                    "Another technician has just selected this test.";

                return RedirectToAction(nameof(ProcessTests));
            }

            test.Status = "InProgress";
            test.LabTechnicianId = technician.LabTechnicianId;
            test.StartDateTime = now;

            foreach (var item in consumables)
            {
                item.Consumable.QuantityInHand -= item.QuantityNeeded;
            }

            if (test.TestRequest.Status == "Submitted" ||
                test.TestRequest.Status == "SamplesReceived")
            {
                test.TestRequest.Status = "InProgress";
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["ResultSuccess"] =
                $"{test.TestType.TestTypeName} for Request " +
                $"#{test.TestRequestId} was added to My Tests " +
                "and is now In Progress.";

            return RedirectToAction(nameof(MyTests));
        }


        // ==========================================
        // MY TESTS
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> MyTests(
            string? dueStatus,
            string? urgency)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
                return NotFound("Lab technician profile not found.");

            var tests = await _context.TestRequestTestTypes
                .AsNoTracking()
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                    .ThenInclude(tt => tt.Category)
                .Where(t =>
                    t.LabTechnicianId == technician.LabTechnicianId &&
                    t.Status == "InProgress")
                .OrderBy(t => t.TestRequestId)
                .ToListAsync();

            var now = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(urgency))
            {
                tests = tests
                    .Where(t => t.TestRequest.Urgency == urgency)
                    .ToList();
            }

            if (dueStatus == "Overdue")
            {
                tests = tests
                    .Where(t =>
                        t.StartDateTime.HasValue &&
                        t.StartDateTime.Value
                            .AddHours(t.TestType.TurnAroundTime) < now)
                    .ToList();
            }
            else if (dueStatus == "Nearing")
            {
                tests = tests
                    .Where(t =>
                        t.StartDateTime.HasValue &&
                        t.StartDateTime.Value
                            .AddHours(t.TestType.TurnAroundTime) >= now &&
                        t.StartDateTime.Value
                            .AddHours(t.TestType.TurnAroundTime) <= now.AddHours(2))
                    .ToList();
            }

            return View(tests);
        }


        // ==========================================
        // CAPTURE TEST RESULT
        // ==========================================
        // FIX: Strongly-typed ViewBag data + null-safe patient lookup.
        // Property names on PatientAllergy.Allergy, PatientCondition.Condition,
        // PatientMedication.Medication are "Name" per the models.
        [HttpGet]
        public async Task<IActionResult> CaptureTestResult(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Doctor)
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Samples)
                        .ThenInclude(s => s.SampleType)
                .Include(t => t.TestType)
                    .ThenInclude(tt => tt.Category)
                .Include(t => t.LabTechnician)
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == id);

            if (test == null)
            {
                return NotFound("Test could not be found.");
            }

            if (test.LabTechnicianId != technician.LabTechnicianId)
            {
                TempData["Error"] =
                    "You are not authorised to capture the result for this test.";

                return RedirectToAction(nameof(MyTests));
            }

            if (test.Status != "InProgress")
            {
                TempData["Error"] =
                    "This test is not currently being processed.";

                return RedirectToAction(nameof(MyTests));
            }

            var patientId = await _context.TestRequests
                .Where(r => r.TestRequestId == test.TestRequestId)
                .Select(r => r.PatientID)
                .FirstOrDefaultAsync();

            var patient = await _context.Patients
                .Where(p => p.PatientID == patientId)
                .Select(p => new
                {
                    p.PatientID,
                    p.Name,
                    p.Surname,
                    p.Gender,
                    p.IDNumber,
                    p.DateOfBirth,
                    p.PhoneNumber
                })
                .FirstOrDefaultAsync();

            // Default empty collections so the view never receives null.
            ViewBag.Patient = patient;
            ViewBag.PatientConditions = new List<PatientCondition>();
            ViewBag.PatientAllergies = new List<PatientAllergy>();
            ViewBag.PatientMedications = new List<PatientMedication>();

            if (patient == null)
            {
                TempData["Error"] =
                    "Patient record could not be found. Please return to your test list.";

                return View(test);
            }

            ViewBag.PatientConditions = await _context.PatientConditions
                .Include(x => x.Condition)
                .Where(x => x.PatientID == patientId)
                .ToListAsync();

            ViewBag.PatientAllergies = await _context.PatientAllergies
                .Include(x => x.Allergy)
                .Where(x => x.PatientID == patientId)
                .ToListAsync();

            ViewBag.PatientMedications = await _context.PatientMedications
                .Include(x => x.Medication)
                .Where(x => x.PatientID == patientId)
                .ToListAsync();

            return View(test);
        }


        // ==========================================
        // SAVE TEST RESULT
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTestResult(
            int testRequestTestTypeId,
            string resultValue,
            string notes)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestType)
                .Include(t => t.TestRequest)
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == testRequestTestTypeId);

            if (test == null)
            {
                return NotFound("Test could not be found.");
            }

            if (test.LabTechnicianId != technician.LabTechnicianId)
            {
                TempData["Error"] =
                    "You are not authorised to capture the result for this test.";

                return RedirectToAction(nameof(ProcessTests));
            }

            if (test.Status != "InProgress")
            {
                TempData["Error"] =
                    "This test is not currently being processed.";

                return RedirectToAction(nameof(MyTests));
            }

            if (string.IsNullOrWhiteSpace(resultValue))
            {
                TempData["Error"] = "Please enter a result value.";

                return RedirectToAction(
                    nameof(CaptureTestResult),
                    new { id = testRequestTestTypeId });
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                TempData["Error"] =
                    "Please enter notes for the test result.";

                return RedirectToAction(
                    nameof(CaptureTestResult),
                    new { id = testRequestTestTypeId });
            }

            if (!double.TryParse(
                resultValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double numericResult))
            {
                TempData["Error"] =
                    "Please enter a valid numeric result.";

                return RedirectToAction(
                    nameof(CaptureTestResult),
                    new { id = testRequestTestTypeId });
            }

            double normalMin = test.TestType.NormalRangeMin;
            double normalMax = test.TestType.NormalRangeMax;

            bool isAbnormal =
                numericResult < normalMin ||
                numericResult > normalMax;

            var testResult = new TestResult
            {
                ResultValue = resultValue.Trim(),
                Notes = notes.Trim(),
                IsAbnormal = isAbnormal,
                DateCaptured = DateTime.Now
            };

            _context.TestResults.Add(testResult);

            await _context.SaveChangesAsync();

            test.TestResultId = testResult.TestResultId;
            test.Status = "Completed";
            test.CompletionDateTime = DateTime.Now;

            AddTestResultHistory(
                test,
                technician.LabTechnicianId,
                "Result Captured",
                "InProgress",
                "Completed",
                testResult);

            bool allTestsCompleted =
                !await _context.TestRequestTestTypes
                    .AnyAsync(t =>
                        t.TestRequestId == test.TestRequestId &&
                        t.TestRequestTestTypeId != test.TestRequestTestTypeId &&
                        t.Status != "Completed" &&
                        t.Status != "Verified");

            if (allTestsCompleted)
            {
                test.TestRequest.Status = "Completed";
            }

            await _context.SaveChangesAsync();

            TempData["ResultSuccess"] =
                "Results successfully processed.";

            return RedirectToAction(nameof(MyTests));
        }


        // ==========================================
        // VERIFY RESULTS
        // ==========================================

        public async Task<IActionResult> VerifyResults()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var tests = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .Include(t => t.TestResult)
                .Where(t =>
                    t.Status == "Completed" &&
                    t.TestResultId != null &&
                    t.LabTechnicianId != technician.LabTechnicianId &&
                    _context.TechnicianTestTypes
                        .Any(tt =>
                            tt.LabTechnicianId == technician.LabTechnicianId &&
                            tt.TestTypeId == t.TestTypeId))
                .ToListAsync();

            var results = tests
                .Where(t => t.TestResult != null)
                .Select(t => t.TestResult!)
                .Distinct()
                .ToList();

            foreach (var result in results)
            {
                result.TestRequestTestType =
                    tests
                        .Where(t =>
                            t.TestResultId == result.TestResultId)
                        .ToList();
            }

            return View(results);
        }


        public async Task<IActionResult> VerifyResultDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var result = await _context.TestResults
                .Include(r => r.TestRequestTestType)
                    .ThenInclude(t => t.TestRequest)
                        .ThenInclude(r => r.Patient)
                .Include(r => r.TestRequestTestType)
                    .ThenInclude(t => t.TestType)
                .FirstOrDefaultAsync(r => r.TestResultId == id);

            if (result == null)
            {
                return NotFound("Test result could not be found.");
            }

            var test = result.TestRequestTestType.FirstOrDefault();

            if (test == null)
            {
                return NotFound(
                    "The test associated with this result could not be found.");
            }

            var patient = test.TestRequest?.Patient;

            if (patient == null)
            {
                return NotFound("Patient information could not be found.");
            }

            if (test.LabTechnicianId == technician.LabTechnicianId)
            {
                TempData["Error"] =
                    "You cannot verify a result that you performed yourself.";

                return RedirectToAction(nameof(VerifyResults));
            }

            var canVerify = await _context.TechnicianTestTypes
                .AnyAsync(tt =>
                    tt.LabTechnicianId == technician.LabTechnicianId &&
                    tt.TestTypeId == test.TestTypeId);

            if (!canVerify)
            {
                TempData["Error"] =
                    "You are not assigned to verify this test type.";

                return RedirectToAction(nameof(VerifyResults));
            }

            if (test.Status != "Completed")
            {
                TempData["Error"] =
                    "This test result is not currently waiting for verification.";

                return RedirectToAction(nameof(VerifyResults));
            }

            ViewBag.NormalRangeMin = test.TestType?.NormalRangeMin;
            ViewBag.NormalRangeMax = test.TestType?.NormalRangeMax;
            ViewBag.TestUnit = test.TestType?.UnitOfMeasurement;

            ViewBag.PatientConditions = await _context.PatientConditions
                .Include(pc => pc.Condition)
                .Where(pc => pc.PatientID == patient.PatientID)
                .ToListAsync();

            ViewBag.PatientAllergies = await _context.PatientAllergies
                .Include(pa => pa.Allergy)
                .Where(pa => pa.PatientID == patient.PatientID)
                .ToListAsync();

            ViewBag.PatientMedications = await _context.PatientMedications
                .Include(pm => pm.Medication)
                .Where(pm => pm.PatientID == patient.PatientID)
                .ToListAsync();

            ViewBag.TestResultHistory = await _context.TestResultHistories
                .Include(h => h.LabTechnician)
                    .ThenInclude(t => t.User)
                .Where(h =>
                    h.TestRequestTestTypeId == test.TestRequestTestTypeId)
                .OrderBy(h => h.ActionDateTime)
                .ToListAsync();

            return View(result);
        }


        [HttpPost]
        public async Task<IActionResult> VerifyResult(
            int id,
            string action,
            string reviewNotes)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var result = await _context.TestResults
                .Include(r => r.TestRequestTestType)
                .FirstOrDefaultAsync(r => r.TestResultId == id);

            if (result == null)
            {
                return NotFound("Test result could not be found.");
            }

            var test = result.TestRequestTestType.FirstOrDefault();

            if (test == null)
            {
                return NotFound(
                    "The test associated with this result could not be found.");
            }

            if (test.LabTechnicianId == technician.LabTechnicianId)
            {
                TempData["Error"] =
                    "You cannot verify a result that you performed yourself.";

                return RedirectToAction(nameof(VerifyResults));
            }

            var canVerify = await _context.TechnicianTestTypes
                .AnyAsync(tt =>
                    tt.LabTechnicianId == technician.LabTechnicianId &&
                    tt.TestTypeId == test.TestTypeId);

            if (!canVerify)
            {
                TempData["Error"] =
                    "You are not assigned to verify this test type.";

                return RedirectToAction(nameof(VerifyResults));
            }

            if (test.Status != "Completed")
            {
                TempData["Error"] =
                    "This test result is not currently waiting for verification.";

                return RedirectToAction(nameof(VerifyResults));
            }

            var verification = await _context.TestResultVerifications
                .FirstOrDefaultAsync(v => v.TestResultId == result.TestResultId);


            if (action == "Verify")
            {
                test.Status = "Verified";

                if (verification == null)
                {
                    verification = new TestResultVerification
                    {
                        TestResultId = result.TestResultId,
                        VerifiedByTechnicianId = technician.LabTechnicianId,
                        VerificationDateTime = DateTime.Now,
                        Status = "Verified",
                        ReviewNotes = reviewNotes ?? ""
                    };

                    _context.TestResultVerifications.Add(verification);
                }
                else
                {
                    verification.VerifiedByTechnicianId =
                        technician.LabTechnicianId;

                    verification.VerificationDateTime = DateTime.Now;
                    verification.Status = "Verified";
                    verification.ReviewNotes = reviewNotes ?? "";
                }

                AddTestResultHistory(
                    test,
                    technician.LabTechnicianId,
                    "Result Verified",
                    "Completed",
                    "Verified",
                    result,
                    reviewNotes);

                // Save the verification first.
                await _context.SaveChangesAsync();

                // Check whether every test in this request is verified.
                int requestId = test.TestRequestId;

                bool allTestsVerified = await _context.TestRequestTestTypes
                    .Where(t => t.TestRequestId == requestId)
                    .AnyAsync()
                    &&
                    await _context.TestRequestTestTypes
                        .Where(t => t.TestRequestId == requestId)
                        .AllAsync(t => t.Status == "Verified");

                if (allTestsVerified)
                {
                    // Load the request, patient, doctor and all test results.
                    var request = await _context.TestRequests
                        .Include(r => r.Patient)
                        .Include(r => r.Doctor)
                            .ThenInclude(d => d.User)
                        .FirstOrDefaultAsync(r => r.TestRequestId == requestId);

                    var requestTests = await _context.TestRequestTestTypes
                        .Include(t => t.TestType)
                        .Include(t => t.TestResult)
                        .Where(t => t.TestRequestId == requestId)
                        .ToListAsync();

                    if (request == null)
                    {
                        TempData["Error"] =
                            "The results were verified, but the request could not be found for emailing.";

                        return RedirectToAction(nameof(VerifyResults));
                    }

                    var doctorEmailResult = await _context.Database
     .SqlQuery<string>($"""
        SELECT Email AS [Value]
        FROM dbo.Doctors
        WHERE DoctorId = {request.DoctorId}
    """)
     .FirstOrDefaultAsync();

                    string doctorEmail = doctorEmailResult ?? "";

                    string doctorName = (
                        (request.Doctor?.Name ?? "") + " " +
                        (request.Doctor?.Surname ?? "")
                    ).Trim();

                    string patientName = (
                        (request.Patient?.Name ?? "") + " " +
                        (request.Patient?.Surname ?? "")
                    ).Trim();

                    if (string.IsNullOrWhiteSpace(doctorEmail))
                    {
                        TempData["Error"] =
                            "All tests are verified, but the requesting doctor's email address is missing.";

                        return RedirectToAction(nameof(VerifyResults));
                    }

                    // Generate a PDF containing this request's results only.
                    var document = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(35);
                            page.DefaultTextStyle(style => style.FontSize(10));

                            page.Header().Column(header =>
                            {
                                header.Item()
                                    .Text("HEMACORE")
                                    .FontSize(20)
                                    .Bold();

                                header.Item()
                                    .Text("Verified Laboratory Results")
                                    .FontSize(15)
                                    .Bold();
                            });

                            page.Content()
                                .PaddingVertical(15)
                                .Column(column =>
                                {
                                    column.Spacing(8);

                                    column.Item().Text(
                                        $"Request number: {requestId}");

                                    column.Item().Text(
                                        $"Patient: {patientName}");

                                    column.Item().Text(
                                        $"Requesting doctor: {doctorName}");

                                    column.Item().Text(
                                        $"Request date: {request.RequestDate:dd MMM yyyy}");

                                    column.Item().Text(
                                        $"Urgency: {request.Urgency}");

                                    column.Item()
                                        .PaddingTop(10)
                                        .Text("Test Results")
                                        .FontSize(13)
                                        .Bold();

                                    column.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(1);
                                            columns.RelativeColumn(2);
                                        });

                                        table.Header(header =>
                                        {
                                            foreach (var heading in new[]
                                            {
                                    "Test",
                                    "Result",
                                    "Unit",
                                    "Normal range"
                                            })
                                            {
                                                header.Cell()
                                                    .Background(Colors.Grey.Lighten3)
                                                    .Padding(5)
                                                    .Text(heading)
                                                    .Bold();
                                            }
                                        });

                                        foreach (var item in requestTests)
                                        {
                                            string normalRange =
                                                item.TestType?.NormalRangeMin != null &&
                                                item.TestType?.NormalRangeMax != null
                                                    ? $"{item.TestType.NormalRangeMin} - {item.TestType.NormalRangeMax}"
                                                    : "N/A";

                                            table.Cell()
                                                .Padding(5)
                                                .Text(item.TestType?.TestTypeName ?? "N/A");

                                            var resultCell = table.Cell().Padding(5);

                                            string resultValue =
                                                item.TestResult?.ResultValue ?? "N/A";

                                            if (item.TestResult?.IsAbnormal == true)
                                            {
                                                resultCell.Text(
                                                    $"{resultValue} (Abnormal)")
                                                    .FontColor(Colors.Red.Darken2)
                                                    .Bold();
                                            }
                                            else
                                            {
                                                resultCell.Text(resultValue);
                                            }

                                            table.Cell()
                                                .Padding(5)
                                                .Text(item.TestType?.UnitOfMeasurement ?? "N/A");

                                            table.Cell()
                                                .Padding(5)
                                                .Text(normalRange);
                                        }
                                    });

                                    column.Item()
                                        .PaddingTop(12)
                                        .Text(
                                            "These results have been verified by the laboratory. " +
                                            "The requesting doctor must review and release them " +
                                            "to the patient through HemaCore.");

                                    column.Item()
                                        .Text("Confidential medical information.")
                                        .Italic();
                                });

                            page.Footer()
                                .AlignCenter()
                                .Text("HemaCore | Verified Laboratory Results");
                        });
                    });

                    byte[] pdfBytes = document.GeneratePdf();

                    // Email the PDF to the requesting doctor.
                    try
                    {
                        await _emailService.SendVerifiedResultsEmailAsync(
                            doctorEmail,
                            string.IsNullOrWhiteSpace(doctorName)
                                ? "Doctor"
                                : doctorName,
                            string.IsNullOrWhiteSpace(patientName)
                                ? "Patient"
                                : patientName,
                            requestId,
                            pdfBytes);

                        TempData["ResultSuccess"] =
                            "All tests are verified. The results PDF has been emailed to the requesting doctor.";
                    }
                    catch (Exception)
                    {
                        TempData["Error"] =
                            "All tests are verified, but the email could not be sent. Please check the email configuration.";
                    }
                }
                else
                {
                    TempData["ResultSuccess"] =
                        "Test result has been successfully verified.";
                }

                return RedirectToAction(nameof(VerifyResults));
            }

            if (action == "SendBack")
            {
                if (string.IsNullOrWhiteSpace(reviewNotes))
                {
                    TempData["Error"] =
                        "Please provide a reason before sending the result back for review.";

                    return RedirectToAction(
                        nameof(VerifyResultDetails),
                        new { id = result.TestResultId });
                }

                test.Status = "To Be Reviewed";

                if (verification == null)
                {
                    verification = new TestResultVerification
                    {
                        TestResultId = result.TestResultId,
                        VerifiedByTechnicianId = technician.LabTechnicianId,
                        VerificationDateTime = DateTime.Now,
                        Status = "To Be Reviewed",
                        ReviewNotes = reviewNotes
                    };

                    _context.TestResultVerifications.Add(verification);
                }
                else
                {
                    verification.VerifiedByTechnicianId =
                        technician.LabTechnicianId;

                    verification.VerificationDateTime = DateTime.Now;
                    verification.Status = "To Be Reviewed";
                    verification.ReviewNotes = reviewNotes;
                }

                AddTestResultHistory(
                    test,
                    technician.LabTechnicianId,
                    "Sent Back for Review",
                    "Completed",
                    "To Be Reviewed",
                    result,
                    reviewNotes);

                await _context.SaveChangesAsync();

                TempData["ResultSuccess"] =
                    "Test result has been sent back to the original technician for review.";

                return RedirectToAction(nameof(VerifyResults));
            }

            return BadRequest("Invalid verification action.");
        }


        // ==========================================
        // REVIEW RESULTS
        // ==========================================

        public async Task<IActionResult> ReviewResults()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var verifications = await _context.TestResultVerifications
                .Include(v => v.TestResult)
                .Where(v =>
                    v.Status == "To Be Reviewed" &&
                    v.TestResult != null &&
                    _context.TestRequestTestTypes
                        .Any(t =>
                            t.TestResultId == v.TestResultId &&
                            t.LabTechnicianId == technician.LabTechnicianId &&
                            t.Status == "To Be Reviewed"))
                .ToListAsync();

            foreach (var verification in verifications)
            {
                if (verification.TestResult != null)
                {
                    verification.TestResult.TestRequestTestType =
                        await _context.TestRequestTestTypes
                            .Include(t => t.TestRequest)
                                .ThenInclude(r => r.Patient)
                            .Include(t => t.TestType)
                            .Where(t =>
                                t.TestResultId == verification.TestResult.TestResultId &&
                                t.LabTechnicianId == technician.LabTechnicianId &&
                                t.Status == "To Be Reviewed")
                            .ToListAsync();
                }
            }

            return View(verifications);
        }


        [HttpGet]
        public async Task<IActionResult> ReviewResultDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var verification = await _context.TestResultVerifications
                .Include(v => v.TestResult)
                .FirstOrDefaultAsync(v =>
                    v.TestResultVerificationId == id &&
                    v.Status == "To Be Reviewed");

            if (verification?.TestResult == null)
            {
                return NotFound("The review record could not be found.");
            }

            var result = verification.TestResult;

            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Doctor)
                .Include(t => t.TestType)
                .FirstOrDefaultAsync(t =>
                    t.TestResultId == result.TestResultId &&
                    t.LabTechnicianId == technician.LabTechnicianId &&
                    t.Status == "To Be Reviewed");

            if (test == null)
            {
                return NotFound(
                    "The associated test could not be found for this technician.");
            }

            result.TestRequestTestType = new List<TestRequestItem> { test };

            var patient = test.TestRequest?.Patient;

            if (patient == null)
            {
                return NotFound("Patient information could not be found.");
            }

            ViewBag.PatientConditions = await _context.PatientConditions
                .Include(pc => pc.Condition)
                .Where(pc => pc.PatientID == patient.PatientID)
                .ToListAsync();

            ViewBag.PatientAllergies = await _context.PatientAllergies
                .Include(pa => pa.Allergy)
                .Where(pa => pa.PatientID == patient.PatientID)
                .ToListAsync();

            ViewBag.PatientMedications = await _context.PatientMedications
                .Include(pm => pm.Medication)
                .Where(pm => pm.PatientID == patient.PatientID)
                .ToListAsync();

            ViewBag.NormalRangeMin = test.TestType.NormalRangeMin;
            ViewBag.NormalRangeMax = test.TestType.NormalRangeMax;
            ViewBag.TestUnit = test.TestType.UnitOfMeasurement;

            ViewBag.TestResultHistory = await _context.TestResultHistories
                .Include(h => h.LabTechnician)
                    .ThenInclude(t => t.User)
                .Where(h =>
                    h.TestRequestTestTypeId == test.TestRequestTestTypeId)
                .OrderByDescending(h => h.ActionDateTime)
                .ToListAsync();

            return View(verification);
        }


        [HttpPost]
        public async Task<IActionResult> CompleteReview(
            int id,
            string resultValue,
            string notes)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var verification = await _context.TestResultVerifications
                .Include(v => v.TestResult)
                .FirstOrDefaultAsync(v =>
                    v.TestResultVerificationId == id);

            if (verification == null)
            {
                TempData["Error"] = "The review record could not be found.";
                return RedirectToAction(nameof(ReviewResults));
            }

            if (verification.TestResult == null)
            {
                TempData["Error"] = "The test result could not be found.";
                return RedirectToAction(nameof(ReviewResults));
            }

            var result = verification.TestResult;

            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestType)
                .Include(t => t.TestRequest)
                .FirstOrDefaultAsync(t =>
                    t.TestResultId == result.TestResultId);

            if (test == null)
            {
                TempData["Error"] =
                    "The test associated with this result could not be found.";

                return RedirectToAction(nameof(ReviewResults));
            }

            if (test.LabTechnicianId != technician.LabTechnicianId)
            {
                TempData["Error"] =
                    "You are not authorised to review this test result.";

                return RedirectToAction(nameof(ReviewResults));
            }

            if (test.Status != "To Be Reviewed")
            {
                TempData["Error"] =
                    "This test result is no longer waiting for review.";

                return RedirectToAction(nameof(ReviewResults));
            }

            if (string.IsNullOrWhiteSpace(resultValue))
            {
                TempData["Error"] = "Please enter a result value.";

                return RedirectToAction(
                    nameof(ReviewResultDetails),
                    new { id = verification.TestResultVerificationId });
            }

            if (!double.TryParse(
                resultValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double numericResult))
            {
                TempData["Error"] = "Please enter a valid numeric result.";

                return RedirectToAction(
                    nameof(ReviewResultDetails),
                    new { id = verification.TestResultVerificationId });
            }

            double normalMin = test.TestType.NormalRangeMin;
            double normalMax = test.TestType.NormalRangeMax;

            bool isAbnormal =
                numericResult < normalMin ||
                numericResult > normalMax;

            result.ResultValue = resultValue.Trim();
            result.Notes = notes?.Trim() ?? "";
            result.IsAbnormal = isAbnormal;
            result.DateCaptured = DateTime.Now;

            test.Status = "Completed";
            test.CompletionDateTime = DateTime.Now;

            verification.Status = "Resubmitted";
            verification.VerificationDateTime = DateTime.Now;

            AddTestResultHistory(
                test,
                technician.LabTechnicianId,
                "Corrected Result Resubmitted",
                "To Be Reviewed",
                "Completed",
                result,
                verification.ReviewNotes);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "The corrected result has been resubmitted for verification.";

            return RedirectToAction(nameof(ReviewResults));
        }


        // ==========================================
        // REPORTS
        // ==========================================

        public async Task<IActionResult> Reports(
            DateTime? fromDate,
            DateTime? toDate,
            string testType,
            string status,
            string urgency)
        {
            var query = _context.TestRequestTestTypes
                .AsNoTracking()
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(t =>
                    t.CompletionDateTime.HasValue &&
                    t.CompletionDateTime.Value.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(t =>
                    t.CompletionDateTime.HasValue &&
                    t.CompletionDateTime.Value.Date <= toDate.Value.Date);
            }

            if (!string.IsNullOrEmpty(testType))
            {
                query = query.Where(t =>
                    t.TestType.TestTypeName == testType);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }

            if (!string.IsNullOrEmpty(urgency))
            {
                query = query.Where(t =>
                    t.TestRequest.Urgency == urgency);
            }

            var tests = await query
                .Select(t => new
                {
                    t.TestRequestId,

                    PatientName =
                        ((string?)t.TestRequest.Patient.Name ?? "") +
                        " " +
                        ((string?)t.TestRequest.Patient.Surname ?? ""),

                    TestTypeName =
                        (string?)t.TestType.TestTypeName,

                    TechnicianName =
                        ((string?)t.LabTechnician.Name ?? "") +
                        " " +
                        ((string?)t.LabTechnician.Surname ?? ""),

                    Urgency = (string?)t.TestRequest.Urgency,
                    Status = (string?)t.Status,

                    ResultValue = (string?)t.TestResult.ResultValue,
                    IsAbnormal = (bool?)t.TestResult.IsAbnormal,

                    t.StartDateTime,
                    t.CompletionDateTime
                })
                .ToListAsync();

            var report = new LabTechnicianReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                TestType = testType,
                Status = status,
                Urgency = urgency,

                TotalTests = tests.Count,

                CompletedTests = tests.Count(t =>
                    t.Status == "Completed"),

                InProgressTests = tests.Count(t =>
                    t.Status == "InProgress"),

                AbnormalResults = tests.Count(t =>
                    t.IsAbnormal == true),

                Tests = tests.Select(t =>
                    new LabTechnicianReportItemViewModel
                    {
                        TestRequestId = t.TestRequestId,

                        PatientName = t.PatientName.Trim(),

                        TestTypeName = t.TestTypeName ?? "N/A",

                        TechnicianName = t.TechnicianName.Trim(),

                        Urgency = t.Urgency ?? "N/A",

                        Status = t.Status ?? "N/A",

                        ResultValue = t.ResultValue ?? "N/A",

                        IsAbnormal = t.IsAbnormal ?? false,

                        StartDateTime = t.StartDateTime,

                        CompletionDateTime = t.CompletionDateTime,

                        TurnaroundMinutes =
                            t.StartDateTime.HasValue &&
                            t.CompletionDateTime.HasValue
                                ? (t.CompletionDateTime.Value -
                                   t.StartDateTime.Value).TotalMinutes
                                : null
                    })
                    .ToList()
            };

            return View(report);
        }


        // ==========================================
        // EXPORT REPORT PDF
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> ExportReportPdf(
            DateTime? fromDate,
            DateTime? toDate,
            string? testType,
            string? status,
            string? urgency)
        {
            var generatedAt = DateTime.Now;

            var query = _context.TestRequestTestTypes
                .AsNoTracking()
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .Include(t => t.TestResult)
                .Include(t => t.LabTechnician)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                var start = fromDate.Value.Date;

                query = query.Where(t =>
                    t.CompletionDateTime.HasValue &&
                    t.CompletionDateTime.Value >= start);
            }

            if (toDate.HasValue)
            {
                var endExclusive = toDate.Value.Date.AddDays(1);

                query = query.Where(t =>
                    t.CompletionDateTime.HasValue &&
                    t.CompletionDateTime.Value < endExclusive);
            }

            if (!string.IsNullOrWhiteSpace(testType))
            {
                query = query.Where(t =>
                    t.TestType.TestTypeName == testType);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(t => t.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(urgency))
            {
                query = query.Where(t =>
                    t.TestRequest.Urgency == urgency);
            }

            var tests = await query
                .OrderByDescending(t => t.CompletionDateTime)
                .ThenBy(t => t.TestRequestId)
                .ToListAsync();

            var reportTests = tests.Select(t =>
            {
                var turnaround =
                    t.StartDateTime.HasValue &&
                    t.CompletionDateTime.HasValue
                        ? (double?)(
                            t.CompletionDateTime.Value -
                            t.StartDateTime.Value).TotalMinutes
                        : null;

                return new LabTechnicianReportItemViewModel
                {
                    TestRequestId = t.TestRequestId,

                    PatientName = (
                        (t.TestRequest?.Patient?.Name ?? "") + " " +
                        (t.TestRequest?.Patient?.Surname ?? "")
                    ).Trim(),

                    TestTypeName =
                        t.TestType?.TestTypeName ?? "N/A",

                    TechnicianName = (
                        (t.LabTechnician?.Name ?? "") + " " +
                        (t.LabTechnician?.Surname ?? "")
                    ).Trim(),

                    Urgency =
                        t.TestRequest?.Urgency ?? "N/A",

                    Status =
                        t.Status ?? "N/A",

                    ResultValue =
                        t.TestResult?.ResultValue ?? "N/A",

                    IsAbnormal =
                        t.TestResult?.IsAbnormal ?? false,

                    StartDateTime = t.StartDateTime,
                    CompletionDateTime = t.CompletionDateTime,
                    TurnaroundMinutes = turnaround
                };
            }).ToList();

            var totalTests = reportTests.Count;

            var completedTests = reportTests.Count(t =>
                t.Status == "Completed");

            var inProgressTests = reportTests.Count(t =>
                t.Status == "InProgress");

            var abnormalResults = reportTests.Count(t =>
                t.IsAbnormal);

            var averageTurnaround = reportTests
                .Where(t => t.TurnaroundMinutes.HasValue)
                .Select(t => t.TurnaroundMinutes!.Value)
                .DefaultIfEmpty()
                .Average();

            string FormatMinutes(double minutes)
            {
                var totalMinutes = (int)Math.Round(minutes);

                if (totalMinutes < 60)
                {
                    return $"{totalMinutes} min";
                }

                return $"{totalMinutes / 60}h {totalMinutes % 60}min";
            }

            string FormatDate(DateTime? date)
            {
                return date.HasValue
                    ? date.Value.ToString("dd MMM yyyy")
                    : "All";
            }

            var document = Document.Create(documentContainer =>
            {
                documentContainer.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(style =>
                        style.FontSize(9).FontColor("#263238"));

                    page.Header().Column(header =>
                    {
                        header.Item()
                            .Text("HEMACORE")
                            .FontSize(19)
                            .Bold()
                            .FontColor("#263238");

                        header.Item()
                            .PaddingTop(3)
                            .Text("LABORATORY INFORMATION SYSTEM")
                            .FontSize(8)
                            .FontColor("#52606D");

                        header.Item()
                            .PaddingTop(8)
                            .LineHorizontal(1)
                            .LineColor("#B8C2CC");
                    });

                    page.Content()
                        .PaddingTop(12)
                        .Column(column =>
                        {
                            column.Spacing(10);

                            column.Item().Text(
                                "Lab Technician Test Processing Report")
                                .FontSize(17)
                                .Bold()
                                .FontColor("#1F2933");

                            column.Item().Text(
                                $"Generated on {generatedAt:dd MMMM yyyy} " +
                                $"at {generatedAt:HH:mm}")
                                .FontSize(9);

                            column.Item()
                                .Border(1)
                                .BorderColor("#D9E0E5")
                                .Background("#F5F7F9")
                                .Padding(10)
                                .Column(criteria =>
                                {
                                    criteria.Spacing(4);

                                    criteria.Item().Text("REPORT CRITERIA")
                                        .Bold()
                                        .FontSize(10);

                                    criteria.Item().Text(
                                        $"Completion date range: " +
                                        $"{FormatDate(fromDate)} to " +
                                        $"{FormatDate(toDate)}");

                                    criteria.Item().Text(
                                        $"Test type: " +
                                        $"{(string.IsNullOrWhiteSpace(testType) ? "All" : testType)}");

                                    criteria.Item().Text(
                                        $"Status: " +
                                        $"{(string.IsNullOrWhiteSpace(status) ? "All" : status == "InProgress" ? "In progress" : status)}");

                                    criteria.Item().Text(
                                        $"Urgency: " +
                                        $"{(string.IsNullOrWhiteSpace(urgency) ? "All" : urgency)}");
                                });

                            column.Item()
                                .PaddingTop(2)
                                .Text("SUMMARY")
                                .FontSize(11)
                                .Bold();

                            column.Item().Table(summary =>
                            {
                                summary.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                    cols.RelativeColumn();
                                });

                                void SummaryCell(string label, string value)
                                {
                                    summary.Cell()
                                        .Border(1)
                                        .BorderColor("#D9E0E5")
                                        .Padding(8)
                                        .Column(cell =>
                                        {
                                            cell.Spacing(3);

                                            cell.Item().Text(label)
                                                .FontSize(8)
                                                .FontColor("#52606D");

                                            cell.Item().Text(value)
                                                .FontSize(13)
                                                .Bold();
                                        });
                                }

                                SummaryCell("Total tests",
                                    totalTests.ToString());

                                SummaryCell("Completed",
                                    completedTests.ToString());

                                SummaryCell("In progress",
                                    inProgressTests.ToString());

                                SummaryCell("Abnormal results",
                                    abnormalResults.ToString());

                                SummaryCell("Average turnaround",
                                    reportTests.Any(t =>
                                        t.TurnaroundMinutes.HasValue)
                                            ? FormatMinutes(averageTurnaround)
                                            : "N/A");
                            });

                            column.Item()
                                .PaddingTop(4)
                                .Text("TEST DETAILS")
                                .FontSize(11)
                                .Bold();

                            if (!reportTests.Any())
                            {
                                column.Item()
                                    .Border(1)
                                    .BorderColor("#D9E0E5")
                                    .Padding(18)
                                    .AlignCenter()
                                    .Text(
                                        "No records found for the selected criteria.")
                                    .FontSize(10);
                            }
                            else
                            {
                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.ConstantColumn(52);
                                        cols.RelativeColumn(1.5f);
                                        cols.RelativeColumn(1.4f);
                                        cols.RelativeColumn(1.4f);
                                        cols.ConstantColumn(53);
                                        cols.ConstantColumn(67);
                                        cols.RelativeColumn(1.1f);
                                        cols.ConstantColumn(73);
                                        cols.ConstantColumn(75);
                                    });

                                    table.Header(header =>
                                    {
                                        string[] headings =
                                        {
                                            "Request",
                                            "Patient",
                                            "Test type",
                                            "Technician",
                                            "Urgency",
                                            "Status",
                                            "Result",
                                            "Completed",
                                            "Turnaround"
                                        };

                                        foreach (var heading in headings)
                                        {
                                            header.Cell()
                                                .Background("#E9EDF1")
                                                .BorderBottom(1)
                                                .BorderColor("#B8C2CC")
                                                .PaddingVertical(7)
                                                .PaddingHorizontal(4)
                                                .Text(heading)
                                                .Bold()
                                                .FontSize(8);
                                        }
                                    });

                                    foreach (var test in reportTests)
                                    {
                                        void DataCell(string value)
                                        {
                                            table.Cell()
                                                .BorderBottom(0.5f)
                                                .BorderColor("#E1E6EB")
                                                .PaddingVertical(6)
                                                .PaddingHorizontal(4)
                                                .Text(string.IsNullOrWhiteSpace(value)
                                                    ? "—"
                                                    : value)
                                                .FontSize(8);
                                        }

                                        DataCell($"#{test.TestRequestId}");
                                        DataCell(test.PatientName);
                                        DataCell(test.TestTypeName);
                                        DataCell(test.TechnicianName);
                                        DataCell(test.Urgency);

                                        DataCell(test.Status == "InProgress"
                                            ? "In progress"
                                            : test.Status);

                                        var resultText = test.ResultValue;

                                        if (test.IsAbnormal &&
                                            !string.IsNullOrWhiteSpace(resultText) &&
                                            resultText != "N/A")
                                        {
                                            resultText += " (Abnormal)";
                                        }

                                        DataCell(resultText);

                                        DataCell(test.CompletionDateTime.HasValue
                                            ? test.CompletionDateTime.Value
                                                .ToString("dd/MM/yy")
                                            : "—");

                                        DataCell(test.TurnaroundMinutes.HasValue
                                            ? FormatMinutes(
                                                test.TurnaroundMinutes.Value)
                                            : "—");
                                    }
                                });
                            }
                        });

                    page.Footer()
                        .PaddingTop(8)
                        .BorderTop(1)
                        .BorderColor("#D9E0E5")
                        .Row(row =>
                        {
                            row.RelativeItem().Text(
                                $"HemaCore | Generated {generatedAt:dd/MM/yyyy HH:mm}")
                                .FontSize(8)
                                .FontColor("#52606D");

                            row.ConstantItem(100)
                                .AlignRight()
                                .Text(text =>
                                {
                                    text.Span("Page ")
                                        .FontSize(8);

                                    text.CurrentPageNumber()
                                        .FontSize(8);

                                    text.Span(" of ")
                                        .FontSize(8);

                                    text.TotalPages()
                                        .FontSize(8);
                                });
                        });
                });
            });

            var pdfBytes = document.GeneratePdf();

            return File(
                pdfBytes,
                "application/pdf",
                $"HemaCore_LabTechnicianReport_{generatedAt:yyyyMMdd_HHmmss}.pdf");
        }


        // ==========================================
        // MY COMPLETED TESTS
        // ==========================================

        public async Task<IActionResult> MyCompletedTests()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var completedTests = await _context.TestRequestTestTypes
                .Where(t =>
                    t.LabTechnicianId == technician.LabTechnicianId &&
                    (t.Status == "Completed" || t.Status == "Verified"))
                .OrderByDescending(t => t.CompletionDateTime)
                .ToListAsync();

            foreach (var test in completedTests)
            {
                test.TestRequest = await _context.TestRequests
                    .FirstOrDefaultAsync(r =>
                        r.TestRequestId == test.TestRequestId);

                test.TestType = await _context.TestTypes
                    .FirstOrDefaultAsync(tt =>
                        tt.TestTypeId == test.TestTypeId);
            }

            return View(completedTests);
        }


        public async Task<IActionResult> CompletedTestDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var test = await _context.TestRequestTestTypes
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == id &&
                    t.LabTechnicianId == technician.LabTechnicianId &&
                    (t.Status == "Completed" || t.Status == "Verified"));

            if (test == null)
            {
                return NotFound("Completed test not found.");
            }

            test.TestRequest = await _context.TestRequests
                .FirstOrDefaultAsync(r =>
                    r.TestRequestId == test.TestRequestId);

            test.TestType = await _context.TestTypes
                .FirstOrDefaultAsync(t =>
                    t.TestTypeId == test.TestTypeId);

            if (test.TestRequest == null || test.TestType == null)
            {
                return NotFound("The associated request or test type could not be found.");
            }

            return View(test);
        }


        // ==========================================
        // MY ACCOUNT
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> TechnicianProfile()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var fullName = (user.FullName ?? "").Trim();
            var nameParts = fullName.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            var model = new LabTechnicianProfileViewModel
            {
                Name = nameParts.Length > 0 ? nameParts[0] : "",
                Surname = nameParts.Length > 1
                    ? string.Join(" ", nameParts.Skip(1))
                    : "",
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var fullName = (user.FullName ?? "").Trim();
            var nameParts = fullName.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            var model = new LabTechnicianProfileViewModel
            {
                Name = nameParts.Length > 0 ? nameParts[0] : "",
                Surname = nameParts.Length > 1
                    ? string.Join(" ", nameParts.Skip(1))
                    : "",
                Email = user.Email ?? "",
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(
            LabTechnicianProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var existingUser = await _userManager.FindByEmailAsync(
                model.Email.Trim());

            if (existingUser != null && existingUser.Id != user.Id)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email address is already in use.");

                return View(model);
            }

            user.FullName =
                $"{model.Name.Trim()} {model.Surname.Trim()}".Trim();

            user.Email = model.Email.Trim();
            user.UserName = model.Email.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();

            var identityResult = await _userManager.UpdateAsync(user);

            if (!identityResult.Succeeded)
            {
                foreach (var error in identityResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View(model);
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician != null)
            {
                technician.PhoneNumber = model.PhoneNumber?.Trim();

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] =
                "Your profile has been updated successfully.";

            return RedirectToAction(nameof(TechnicianProfile));
        }


        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = await _userManager.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View(model);
            }

            TempData["AccountSuccess"] =
                "Your password was changed successfully.";

            return RedirectToAction(nameof(TechnicianProfile));
        }


        // ==========================================
        // HELPERS
        // ==========================================

        private void AddTestResultHistory(
            TestRequestItem test,
            int technicianId,
            string action,
            string? previousStatus,
            string? newStatus,
            TestResult? result,
            string? reviewNotes = null)
        {
            var history = new TestResultHistory
            {
                TestRequestTestTypeId = test.TestRequestTestTypeId,
                LabTechnicianId = technicianId,
                Action = action,
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                ResultValue = result?.ResultValue,
                ResultNotes = result?.Notes,
                IsAbnormal = result?.IsAbnormal,
                ReviewNotes = reviewNotes,
                ActionDateTime = DateTime.Now
            };

            _context.TestResultHistories.Add(history);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSampleBarcode(int sampleId, string barcode)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
                return NotFound("Lab technician profile not found.");

            var sample = await _context.Samples
                .FirstOrDefaultAsync(s => s.SampleId == sampleId);

            if (sample == null)
                return NotFound("Sample not found.");

            if (sample.Status != "Pending" || sample.DateTimeReceived.HasValue)
            {
                TempData["SampleError"] =
                    "The barcode can only be corrected before the sample is received or rejected.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            barcode = barcode?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(barcode))
            {
                TempData["SampleError"] = "Please enter a valid barcode.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            if (barcode.Length > 100)
            {
                TempData["SampleError"] = "The barcode cannot exceed 100 characters.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            bool barcodeExists = await _context.Samples.AnyAsync(s =>
                s.Barcode == barcode && s.SampleId != sampleId);

            if (barcodeExists)
            {
                TempData["SampleError"] =
                    "This barcode is already assigned to another sample.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            sample.Barcode = barcode;

            await _context.SaveChangesAsync();

            TempData["SampleReceivedSuccess"] =
                "Barcode corrected successfully.";

            return RedirectToAction(nameof(ReceiveSampleDetails),
                new { id = sample.TestRequestId });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectSample(int sampleId, string rejectionReason)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
                return NotFound("Lab technician profile not found.");

            var sample = await _context.Samples
                .FirstOrDefaultAsync(s => s.SampleId == sampleId);

            if (sample == null)
                return NotFound("Sample not found.");

            if (sample.Status == "Received" || sample.DateTimeReceived.HasValue)
            {
                TempData["SampleError"] =
                    "This sample has already been received and cannot be cancelled.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            if (sample.Status == "Cancelled")
            {
                TempData["SampleError"] =
                    "This sample has already been cancelled.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            rejectionReason = rejectionReason?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["SampleError"] =
                    "A cancellation reason is required.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            if (rejectionReason.Length > 1000)
            {
                TempData["SampleError"] =
                    "The cancellation reason cannot exceed 1000 characters.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            sample.Status = "Cancelled";

            sample.RejectionReason = rejectionReason;
            sample.RejectedDateTime = DateTime.Now;
            sample.RejectedByLabTechnicianId = technician.LabTechnicianId;

            await _context.SaveChangesAsync();

            TempData["SampleReceivedSuccess"] =
                $"Sample {sample.Barcode} was cancelled successfully. The reason has been recorded.";

            return RedirectToAction(nameof(ReceiveSampleDetails),
                new { id = sample.TestRequestId });
        }
    }
}
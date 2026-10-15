
using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Models.ViewModels;
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

        public LabTechnicianController(
            AppDbContext context,
            UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // ==========================================
        // LAB TECHNICIAN DASHBOARD
        // ==========================================

        public async Task<IActionResult> Index()
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
                    TechnicianName = user.FullName,
                    Workload = new List<LabTechnicianWorkloadViewModel>()
                });
            }

            var myTests = _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .Where(t =>
                    t.LabTechnicianId == technician.LabTechnicianId);

            var dashboard = new LabTechnicianDashboardViewModel
            {
                TechnicianName = user.FullName,

                SamplesAwaitingReceipt = await myTests
                    .CountAsync(t =>
                        t.Status != "Completed"),

                TestsAwaitingProcessing = await _context.TestRequestTestTypes
                    .CountAsync(t =>
                        t.Status == "Submitted" &&
                        t.LabTechnicianId == null),

                WaitingVerification = await myTests
                    .CountAsync(t =>
                        t.Status == "Completed"),

                WaitingReview = await _context.TestResultVerifications
                    .CountAsync(v =>
                        v.Status == "To Be Reviewed"),

                UrgentTests = await myTests
                    .CountAsync(t =>
                        t.TestRequest.Urgency == "STAT")
            };

            dashboard.Workload = await myTests
                .Select(t => new LabTechnicianWorkloadViewModel
                {
                    TestRequestId = t.TestRequestId,

                    PatientName =
                        t.TestRequest.Patient.Name + " " +
                        t.TestRequest.Patient.Surname,

                    TestTypeName =
                        t.TestType.TestTypeName,

                    Urgency =
                        t.TestRequest.Urgency,

                    Status =
                        t.Status,

                    DueTime =
                        t.CompletionDateTime
                })
                .ToListAsync();

            dashboard.OverdueTests = dashboard.Workload
                .Count(t =>
                    t.DueTime.HasValue &&
                    t.DueTime.Value < DateTime.Now &&
                    t.Status != "Completed");

            dashboard.NearingTurnaround = dashboard.Workload
                .Count(t =>
                    t.DueTime.HasValue &&
                    t.DueTime.Value > DateTime.Now &&
                    t.DueTime.Value <= DateTime.Now.AddHours(2) &&
                    t.Status != "Completed");

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
                .Where(t =>
                    t.Status == "Submitted" &&
                    t.Samples.Any(s =>
                        s.Status != "Received" &&
                        s.Status != "Rejected" &&
                        !s.DateTimeReceived.HasValue))
                .ToListAsync();

            return View(testRequests);
        }


        // ==========================================
        // RECEIVE SAMPLE - DETAILS
        // ==========================================

        public async Task<IActionResult> ReceiveSampleDetails(int id)
        {
            var testRequest = await _context.TestRequests
                .Include(t => t.Patient)
                .Include(t => t.Doctor)
                .Include(t => t.Samples)
                    .ThenInclude(s => s.SampleType)
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
       


        // The time a technician finishes work. Change 16:30 if needed.
     //   private static readonly TimeSpan ShiftEnd = new TimeSpan(16, 30, 0);


        // ======================================================================
        // STEP 1 - PROCESS TESTS (tests waiting to be selected)
        // ======================================================================

      
// ==========================================
// PROCESS TESTS
// Includes temporary diagnostic counts
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

            // Get the test types this technician is qualified to perform.
            var qualifiedTestTypeIds =
                await _context.TechnicianTestTypes
                    .Where(tt =>
                        tt.LabTechnicianId == technician.LabTechnicianId)
                    .Select(tt => tt.TestTypeId)
                    .Distinct()
                    .ToListAsync();

            // Base query: all existing test records.
            var allTests = _context.TestRequestTestTypes
                .AsNoTracking();

            // Diagnostic stage 1: tests still submitted.
            var submittedTests = allTests
                .Where(t => t.Status == "Submitted");

            // Diagnostic stage 2: tests not yet assigned.
            var unassignedTests = submittedTests
                .Where(t => t.LabTechnicianId == null);

            // Diagnostic stage 3: request is in an eligible state.
            var readyRequests = unassignedTests
                .Where(t =>
                    t.TestRequest.Status == "Submitted" ||
                    t.TestRequest.Status == "SamplesReceived" ||
                    t.TestRequest.Status == "InProgress");

            // Diagnostic stage 4: technician is qualified.
            var qualifiedTests = readyRequests
                .Where(t =>
                    qualifiedTestTypeIds.Contains(t.TestTypeId));

            // Diagnostic stage 5: required sample has been received.
            var receivedSampleTests = qualifiedTests
                .Where(t => _context.Samples.Any(s =>
                    s.TestRequestId == t.TestRequestId &&
                    s.SampleTypeId == t.TestType.SampleTypeId &&
                    s.DateTimeReceived.HasValue));

            // TEMPORARY DIAGNOSTICS
            ViewBag.DebugCounts = new Dictionary<string, int>
            {
                ["All test records"] =
                    await allTests.CountAsync(),

                ["Submitted tests"] =
                    await submittedTests.CountAsync(),

                ["Unassigned submitted tests"] =
                    await unassignedTests.CountAsync(),

                ["Tests with eligible request status"] =
                    await readyRequests.CountAsync(),

                ["Tests matching technician qualifications"] =
                    await qualifiedTests.CountAsync(),

                ["Tests with required sample received"] =
                    await receivedSampleTests.CountAsync(),

                ["Technician qualifications configured"] =
                    qualifiedTestTypeIds.Count
            };

            // Actual Process Tests query.
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

            // Apply the existing filters.
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
                    t.TestRequest.RequestDate
                        .AddMinutes(t.TestType.TurnAroundTime)
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


        // ======================================================================
        // STEP 1 + 2 - TEST DETAILS ("View Details": turnaround + consumables)
        // ======================================================================


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
            var expectedFinish =
                now.AddMinutes(test.TestType.TurnAroundTime);

            ViewBag.Consumables = consumables;
            ViewBag.ExpectedFinish = expectedFinish;

            ViewBag.HasShortage = consumables.Any(tc =>
                tc.Consumable.QuantityInHand < tc.QuantityNeeded);

            return View(test);
        }

        // ======================================================================
        // STEP 3 - ADD TO MY TESTS (saved against the technician, NOT in progress)
        // ======================================================================



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

            // Protect test selection and stock deduction.
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

            // Only unassigned, submitted tests can be accepted.
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

            // Confirm that this technician is qualified for the test.
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

            // Confirm that the required sample has been received.
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

            // Load consumables and check stock before accepting the test.
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

            // Atomically assign the test and start processing.
            // Only one technician can successfully claim it.
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

            // Keep the tracked entity consistent with the database.
            test.Status = "InProgress";
            test.LabTechnicianId = technician.LabTechnicianId;
            test.StartDateTime = now;

            // Deduct consumables once, when the technician accepts the test.
            foreach (var item in consumables)
            {
                item.Consumable.QuantityInHand -= item.QuantityNeeded;
            }

            // Move the request into progress when the test is accepted.
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

        // ======================================================================
        // STEP 4 - MY TESTS (accepted tests: not started + in progress)
        // ======================================================================


        [HttpGet]
        public async Task<IActionResult> MyTests()
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
                    (t.Status == "InProgress" ||
                     t.Status == "To Be Reviewed"))
                .OrderBy(t => t.Status == "To Be Reviewed" ? 0 : 1)
                .ThenBy(t => t.TestRequestId)
                .ToListAsync();

            return View(tests);
        }


        // ======================================================================
        // STEP 4 - START PROCESSING (from My Tests; then on to Capture Result)
        // ======================================================================

       

        [HttpGet]
public async Task<IActionResult> CaptureTestResult(int id)
        {
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
                return NotFound();
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

            if (patient == null)
            {
                return NotFound("Patient record not found.");
            }

            ViewBag.Patient = patient;

            var conditions = await _context.PatientConditions
                .Include(x => x.Condition)
                .Where(x => x.PatientID == patientId)
                .ToListAsync();

            var allergies = await _context.PatientAllergies
                .Include(x => x.Allergy)
                .Where(x => x.PatientID == patientId)
                .ToListAsync();

            var medications = await _context.PatientMedications
                .Include(x => x.Medication)
                .Where(x => x.PatientID == patientId)
                .ToListAsync();

            ViewBag.PatientConditions = conditions;
            ViewBag.PatientAllergies = allergies;
            ViewBag.PatientMedications = medications;

            return View(test);
        }




        
[HttpPost]
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

                return RedirectToAction(nameof(ProcessTests));
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

            // Link the saved result to the test.
            test.TestResultId = testResult.TestResultId;
            test.Status = "Completed";
            test.CompletionDateTime = DateTime.Now;

            // Record the result capture in the history table.
            AddTestResultHistory(
                test,
                technician.LabTechnicianId,
                "Result Captured",
                "InProgress",
                "Completed",
                testResult);

            // Check whether all tests for this request are completed.
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

            return RedirectToAction(nameof(ProcessTests));
        }

        public async Task<IActionResult> VerifyResults()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var technician =
                await _context.LabTechnicians
                    .FirstOrDefaultAsync(t =>
                        t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound(
                    "Lab technician profile not found.");
            }

            var tests =
                await _context.TestRequestTestTypes
                    .Include(t => t.TestRequest)
                        .ThenInclude(r => r.Patient)
                    .Include(t => t.TestType)
                    .Include(t => t.TestResult)
                    .Where(t =>
                        t.Status == "Completed" &&
                        t.TestResultId != null &&
                        t.LabTechnicianId !=
                            technician.LabTechnicianId &&
                        _context.TechnicianTestTypes
                            .Any(tt =>
                                tt.LabTechnicianId ==
                                    technician.LabTechnicianId &&
                                tt.TestTypeId ==
                                    t.TestTypeId))
                    .ToListAsync();

            var results =
                tests
                    .Where(t =>
                        t.TestResult != null)
                    .Select(t =>
                        t.TestResult!)
                    .Distinct()
                    .ToList();

            foreach (var result in results)
            {
                result.TestRequestTestType =
                    tests
                        .Where(t =>
                            t.TestResultId ==
                            result.TestResultId)
                        .ToList();
            }

            return View(results);
        }





        public async Task<IActionResult> VerifyResultDetails(int id)
        {
            // Get the currently logged-in user.
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get the lab technician profile.
            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            // Load the result, test, request and patient.
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

            // Prevent a technician from verifying their own result.
            if (test.LabTechnicianId == technician.LabTechnicianId)
            {
                TempData["Error"] =
                    "You cannot verify a result that you performed yourself.";

                return RedirectToAction(nameof(VerifyResults));
            }

            // Confirm that this technician is qualified to verify the test type.
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

            // Only allow results currently awaiting verification.
            if (test.Status != "Completed")
            {
                TempData["Error"] =
                    "This test result is not currently waiting for verification.";

                return RedirectToAction(nameof(VerifyResults));
            }

            // Make the configured reference range available to the view.
            ViewBag.NormalRangeMin = test.TestType?.NormalRangeMin;
            ViewBag.NormalRangeMax = test.TestType?.NormalRangeMax;
            ViewBag.TestUnit = test.TestType?.UnitOfMeasurement;

            // Load the patient's medical conditions.
            ViewBag.PatientConditions = await _context.PatientConditions
                .Include(pc => pc.Condition)
                .Where(pc => pc.PatientID == patient.PatientID)
                .ToListAsync();

            // Load the patient's allergies.
            ViewBag.PatientAllergies = await _context.PatientAllergies
                .Include(pa => pa.Allergy)
                .Where(pa => pa.PatientID == patient.PatientID)
                .ToListAsync();

            // Load the patient's medications.
            ViewBag.PatientMedications = await _context.PatientMedications
                .Include(pm => pm.Medication)
                .Where(pm => pm.PatientID == patient.PatientID)
                .ToListAsync();

            // Load the review history for this specific test.
            ViewBag.TestResultHistory = await _context.TestResultHistories
                .Include(h => h.LabTechnician)
                    .ThenInclude(t => t.User)
                .Where(h =>
                    h.TestRequestTestTypeId == test.TestRequestTestTypeId)
                .OrderBy(h => h.ActionDateTime)
                .ToListAsync();

            // Display the result, patient information and history.
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

                // Record successful verification.
                AddTestResultHistory(
                    test,
                    technician.LabTechnicianId,
                    "Result Verified",
                    "Completed",
                    "Verified",
                    result,
                    reviewNotes);

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Test result has been successfully verified.";

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

                // Record why the result was sent back.
                AddTestResultHistory(
                    test,
                    technician.LabTechnicianId,
                    "Sent Back for Review",
                    "Completed",
                    "To Be Reviewed",
                    result,
                    reviewNotes);

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Test result has been sent back to the original technician for review.";

                return RedirectToAction(nameof(VerifyResults));
            }

            return BadRequest("Invalid verification action.");
        }

        // ==========================================
        // REVIEW TEST RESULTS - LIST
        // ==========================================

        public async Task<IActionResult> ReviewResults()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var technician =
                await _context.LabTechnicians
                    .FirstOrDefaultAsync(t =>
                        t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound(
                    "Lab technician profile not found.");
            }

            var verifications =
                await _context.TestResultVerifications
                    .Include(v =>
                        v.TestResult)
                    .Where(v =>
                        v.Status ==
                            "To Be Reviewed" &&
                        v.TestResult != null &&
                        _context.TestRequestTestTypes
                            .Any(t =>
                                t.TestResultId ==
                                    v.TestResultId &&
                                t.LabTechnicianId ==
                                    technician.LabTechnicianId &&
                                t.Status ==
                                    "To Be Reviewed"))
                    .ToListAsync();

            foreach (var verification in
                verifications)
            {
                if (verification.TestResult != null)
                {
                    verification.TestResult
                        .TestRequestTestType =
                        await _context
                            .TestRequestTestTypes
                            .Include(t =>
                                t.TestRequest)
                                .ThenInclude(r =>
                                    r.Patient)
                            .Include(t =>
                                t.TestType)
                            .Where(t =>
                                t.TestResultId ==
                                    verification
                                        .TestResult
                                        .TestResultId &&
                                t.LabTechnicianId ==
                                    technician
                                        .LabTechnicianId &&
                                t.Status ==
                                    "To Be Reviewed")
                            .ToListAsync();
                }
            }

            return View(verifications);
        }


        // ==========================================
        // REVIEW RESULT - DETAILS
        // ==========================================

        public async Task<IActionResult> ReviewResultDetails(
            int id)
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var technician =
                await _context.LabTechnicians
                    .FirstOrDefaultAsync(t =>
                        t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound(
                    "Lab technician profile not found.");
            }

            var verification =
                await _context
                    .TestResultVerifications
                    .Include(v =>
                        v.TestResult)
                    .FirstOrDefaultAsync(v =>
                        v.TestResultVerificationId ==
                            id &&
                        v.Status ==
                            "To Be Reviewed");

            if (verification == null)
            {
                return NotFound(
                    "The review record could not be found.");
            }

            if (verification.TestResult != null)
            {
                verification.TestResult
                    .TestRequestTestType =
                    await _context
                        .TestRequestTestTypes
                        .Include(t =>
                            t.TestRequest)
                            .ThenInclude(r =>
                                r.Patient)
                        .Include(t =>
                            t.TestType)
                        .Where(t =>
                            t.TestResultId ==
                                verification
                                    .TestResult
                                    .TestResultId &&
                            t.LabTechnicianId ==
                                technician
                                    .LabTechnicianId &&
                            t.Status ==
                                "To Be Reviewed")
                        .ToListAsync();
            }

            var test =
                verification.TestResult?
                    .TestRequestTestType
                    .FirstOrDefault();

            if (test == null)
            {
                return NotFound(
                    "The associated test could not be found.");
            }

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

            // Update the existing result with the corrected values.
            result.ResultValue = resultValue.Trim();
            result.Notes = notes?.Trim() ?? "";
            result.IsAbnormal = isAbnormal;
            result.DateCaptured = DateTime.Now;

            test.Status = "Completed";
            test.CompletionDateTime = DateTime.Now;

            verification.Status = "Resubmitted";
            verification.VerificationDateTime = DateTime.Now;

            // Record the corrected result in the history table.
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

        public async Task<IActionResult> ExportReportPdf(
            DateTime? fromDate,
            DateTime? toDate,
            string testType,
            string status,
            string urgency)
        {
            var query =
                _context.TestRequestTestTypes
                    .Include(t => t.TestRequest)
                        .ThenInclude(r => r.Patient)
                    .Include(t => t.TestType)
                    .Include(t => t.TestResult)
                    .Include(t => t.LabTechnician)
                    .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(t =>
                    t.CompletionDateTime.HasValue &&
                    t.CompletionDateTime.Value.Date >=
                        fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(t =>
                    t.CompletionDateTime.HasValue &&
                    t.CompletionDateTime.Value.Date <=
                        toDate.Value.Date);
            }

            if (!string.IsNullOrEmpty(testType))
            {
                query = query.Where(t =>
                    t.TestType.TestTypeName ==
                    testType);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t =>
                    t.Status ==
                    status);
            }

            if (!string.IsNullOrEmpty(urgency))
            {
                query = query.Where(t =>
                    t.TestRequest.Urgency ==
                    urgency);
            }

            var tests =
                await query.ToListAsync();

            var reportTests =
                tests.Select(t =>
                    new LabTechnicianReportItemViewModel
                    {
                        TestRequestId =
                            t.TestRequestId,

                        PatientName =
                            (t.TestRequest?.Patient?.Name ?? "") +
                            " " +
                            (t.TestRequest?.Patient?.Surname ?? ""),

                        TestTypeName =
                            t.TestType?.TestTypeName ??
                            "N/A",

                        TechnicianName =
                            (t.LabTechnician?.Name ?? "") +
                            " " +
                            (t.LabTechnician?.Surname ?? ""),

                        Urgency =
                            t.TestRequest?.Urgency ??
                            "N/A",

                        Status =
                            t.Status ??
                            "N/A",

                        ResultValue =
                            t.TestResult?.ResultValue ??
                            "N/A",

                        IsAbnormal =
                            t.TestResult?.IsAbnormal ??
                            false,

                        StartDateTime =
                            t.StartDateTime,

                        CompletionDateTime =
                            t.CompletionDateTime,

                        TurnaroundMinutes =
                            t.StartDateTime.HasValue &&
                            t.CompletionDateTime.HasValue
                                ? (
                                    t.CompletionDateTime.Value -
                                    t.StartDateTime.Value
                                  ).TotalMinutes
                                : null
                    })
                    .ToList();

            var totalTests =
                reportTests.Count;

            var completedTests =
                reportTests.Count(t =>
                    t.Status == "Completed");

            var inProgressTests =
                reportTests.Count(t =>
                    t.Status == "InProgress");

            var abnormalResults =
                reportTests.Count(t =>
                    t.IsAbnormal);

            var document =
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30);

                        page.Header()
                            .Text("HEMACORE")
                            .FontSize(20)
                            .Bold();

                        page.Content()
                            .Column(column =>
                            {
                                column.Spacing(10);

                                column.Item()
                                    .Text(
                                        "LAB TECHNICIAN TEST PROCESSING REPORT")
                                    .FontSize(16)
                                    .Bold();

                                column.Item()
                                    .Text(
                                        $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}");

                                column.Item()
                                    .Text(
                                        $"Date Range: " +
                                        $"{(fromDate.HasValue ? fromDate.Value.ToString("dd/MM/yyyy") : "All")} - " +
                                        $"{(toDate.HasValue ? toDate.Value.ToString("dd/MM/yyyy") : "All")}");

                                column.Item()
                                    .PaddingTop(10)
                                    .Text("REPORT SUMMARY")
                                    .FontSize(13)
                                    .Bold();

                                column.Item()
                                    .Text(
                                        $"Total Tests: {totalTests}");

                                column.Item()
                                    .Text(
                                        $"Completed Tests: {completedTests}");

                                column.Item()
                                    .Text(
                                        $"In Progress: {inProgressTests}");

                                column.Item()
                                    .Text(
                                        $"Abnormal Results: {abnormalResults}");

                                column.Item()
                                    .PaddingTop(10)
                                    .Text("FILTERS APPLIED")
                                    .FontSize(13)
                                    .Bold();

                                column.Item()
                                    .Text(
                                        $"Test Type: {(string.IsNullOrEmpty(testType) ? "All" : testType)}");

                                column.Item()
                                    .Text(
                                        $"Status: {(string.IsNullOrEmpty(status) ? "All" : status)}");

                                column.Item()
                                    .Text(
                                        $"Urgency: {(string.IsNullOrEmpty(urgency) ? "All" : urgency)}");

                                column.Item()
                                    .PaddingTop(15)
                                    .Table(table =>
                                    {
                                        table.ColumnsDefinition(
                                            columns =>
                                            {
                                                columns.ConstantColumn(50);
                                                columns.RelativeColumn(2);
                                                columns.RelativeColumn(2);
                                                columns.RelativeColumn(1.5f);
                                                columns.RelativeColumn(1);
                                                columns.RelativeColumn(1.5f);
                                                columns.RelativeColumn(1.5f);
                                            });

                                        table.Header(header =>
                                        {
                                            header.Cell()
                                                .Text("Request")
                                                .Bold();

                                            header.Cell()
                                                .Text("Patient")
                                                .Bold();

                                            header.Cell()
                                                .Text("Test")
                                                .Bold();

                                            header.Cell()
                                                .Text("Technician")
                                                .Bold();

                                            header.Cell()
                                                .Text("Urgency")
                                                .Bold();

                                            header.Cell()
                                                .Text("Status")
                                                .Bold();

                                            header.Cell()
                                                .Text("Result")
                                                .Bold();
                                        });

                                        foreach (var test in
                                            reportTests)
                                        {
                                            table.Cell()
                                                .Text(
                                                    $"#{test.TestRequestId}");

                                            table.Cell()
                                                .Text(
                                                    test.PatientName);

                                            table.Cell()
                                                .Text(
                                                    test.TestTypeName);

                                            table.Cell()
                                                .Text(
                                                    test.TechnicianName);

                                            table.Cell()
                                                .Text(
                                                    test.Urgency);

                                            table.Cell()
                                                .Text(
                                                    test.Status);

                                            table.Cell()
                                                .Text(
                                                    test.ResultValue);
                                        }
                                    });
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(text =>
                            {
                                text.Span(
                                    "HemaCore - Lab Technician Report | ");

                                text.CurrentPageNumber();
                            });
                    });
                });

            var pdfBytes =
                document.GeneratePdf();

            return File(
                pdfBytes,
                "application/pdf",
                $"LabTechnicianReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
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
                // Load the request without loading the full Patient entity.
                test.TestRequest = await _context.TestRequests
                    .FirstOrDefaultAsync(r =>
                        r.TestRequestId == test.TestRequestId);

                // Load the test type separately.
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
        // MY ACCOUNT - VIEW PROFILE
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> TechnicianProfile()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new ProfileViewModel
            {
                Email = user.Email ?? "",
                FullName = user.FullName ?? "",
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }


        // ==========================================
        // MY ACCOUNT - EDIT PROFILE
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new ProfileViewModel
            {
                Email = user.Email ?? "",
                FullName = user.FullName ?? "",
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(ProfileViewModel model)
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

            // Check whether another user already has this email address.
            var existingUser = await _userManager.FindByEmailAsync(model.Email);

            if (existingUser != null && existingUser.Id != user.Id)
            {
                ModelState.AddModelError(nameof(model.Email),
                    "This email address is already in use.");

                return View(model);
            }

            // Update the Identity user.
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.PhoneNumber = model.PhoneNumber;

            var identityResult = await _userManager.UpdateAsync(user);

            if (!identityResult.Succeeded)
            {
                foreach (var error in identityResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            // Find the technician record linked to this logged-in user.
            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            // Update the technician's phone number as well.
            if (technician != null)
            {
                technician.PhoneNumber = model.PhoneNumber;

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Your profile has been updated successfully.";

            return RedirectToAction(nameof(TechnicianProfile));
        }

        // ==========================================
        // MY ACCOUNT - CHANGE PASSWORD
        // ==========================================

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
                    ModelState.AddModelError("", error.Description);
                }

                return View(model);
            }

            TempData["AccountSuccess"] = "Your password was changed successfully.";

            return RedirectToAction(nameof(TechnicianProfile));
        }



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
                    "This sample has already been received and cannot be rejected through this action.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            if (sample.Status == "Rejected")
            {
                TempData["SampleError"] = "This sample has already been rejected.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            rejectionReason = rejectionReason?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["SampleError"] = "A rejection reason is required.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            if (rejectionReason.Length > 1000)
            {
                TempData["SampleError"] =
                    "The rejection reason cannot exceed 1000 characters.";

                return RedirectToAction(nameof(ReceiveSampleDetails),
                    new { id = sample.TestRequestId });
            }

            sample.Status = "Rejected";
            sample.RejectionReason = rejectionReason;
            sample.RejectedDateTime = DateTime.Now;
            sample.RejectedByLabTechnicianId = technician.LabTechnicianId;

            await _context.SaveChangesAsync();

            TempData["SampleError"] =
                $"Sample {sample.Barcode} was rejected. The reason has been recorded.";

            return RedirectToAction(nameof(ReceiveSampleDetails),
                new { id = sample.TestRequestId });
        }


    }
    }

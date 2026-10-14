
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
                .Where(t => t.Status == "Submitted")
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
        public async Task<IActionResult> ConfirmReceiveSample(int sampleId)
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

            var sample = await _context.Samples
                .Include(s => s.TestRequest)
                    .ThenInclude(r => r.Samples)
                .FirstOrDefaultAsync(s => s.SampleId == sampleId);

            if (sample == null)
            {
                return NotFound();
            }

            // Prevent the same sample from being received twice
            if (sample.DateTimeReceived.HasValue)
            {
                return RedirectToAction(nameof(ReceiveSample));
            }

            // Record the sample receipt
            sample.DateTimeReceived = DateTime.Now;
            sample.LabTechnicianId = technician.LabTechnicianId;

            // Check whether all samples for this request have been received
            bool allReceived = sample.TestRequest.Samples
                .All(s => s.DateTimeReceived.HasValue);

            if (allReceived)
            {
                sample.TestRequest.Status = "SamplesReceived";

                if (sample.TestRequest.Samples.Count == 1)
                {
                    TempData["SampleReceivedSuccess"] =
                        $"Sample {sample.Barcode} has been successfully received.";
                }
                else
                {
                    TempData["SampleReceivedSuccess"] =
                        $"All samples for Request #{sample.TestRequestId} have been successfully received.";
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ReceiveSample));
        }




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
                    .ToListAsync();

            var query = _context.TestRequestTestTypes
     .Include(t => t.TestRequest)
     .Include(t => t.TestType)
         .ThenInclude(tt => tt.Category)
                 .Where(t =>
                    (
                        // New tests the technician is qualified to perform
                        (
                            t.Status == "Submitted" &&
                            (t.TestRequest.Status == "SamplesReceived" ||
                             t.TestRequest.Status == "InProgress") &&
                            qualifiedTestTypeIds.Contains(t.TestTypeId) &&
                            _context.Samples.Any(s =>
                                s.TestRequestId == t.TestRequestId &&
                                s.SampleTypeId == t.TestType.SampleTypeId &&
                                s.DateTimeReceived.HasValue)
                        )

                        ||

                        // Tests already started by this technician
                        (
                            t.Status == "InProgress" &&
                            t.LabTechnicianId == technician.LabTechnicianId
                        )
                    ))
                .AsQueryable();

            if (!string.IsNullOrEmpty(urgency))
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


        [HttpPost]
        public async Task<IActionResult> StartProcessing(int id)
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
                .Include(t => t.TestType)
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == id);

            if (test == null)
            {
                return NotFound("Test could not be found.");
            }

            if (test.Status != "Submitted")
            {
                TempData["Error"] =
                    "This test has already been selected or processed.";

                return RedirectToAction(nameof(ProcessTests));
            }

            if (test.TestRequest.Status != "SamplesReceived" &&
                test.TestRequest.Status != "InProgress")
            {
                TempData["Error"] =
                    "This test cannot be started because the request is not ready for processing.";

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

            // Confirm that the required sample type has been received
            // for this specific test request.
            var requiredSampleReceived = await _context.Samples
                .AnyAsync(s =>
                    s.TestRequestId == test.TestRequestId &&
                    s.SampleTypeId == test.TestType.SampleTypeId &&
                    s.DateTimeReceived.HasValue);

            if (!requiredSampleReceived)
            {
                TempData["Error"] =
                    $"The required {test.TestType.TestTypeName} sample has not been received.";

                return RedirectToAction(nameof(ProcessTests));
            }

            bool firstTestForRequest =
                !await _context.TestRequestTestTypes
                    .AnyAsync(t =>
                        t.TestRequestId == test.TestRequestId &&
                        t.Status == "InProgress");

            test.LabTechnicianId = technician.LabTechnicianId;
            test.Status = "InProgress";
            test.StartDateTime = DateTime.Now;

            if (firstTestForRequest)
            {
                test.TestRequest.Status = "InProgress";
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(CaptureTestResult),
                new { id = test.TestRequestTestTypeId });
        }


     
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

            // Get the lab technician profile linked to the user.
            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            // Retrieve the test result and its related test, patient and test type.
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

            // Get the test associated with this result.
            var test = result.TestRequestTestType.FirstOrDefault();

            if (test == null)
            {
                return NotFound(
                    "The test associated with this result could not be found.");
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

            // Only allow results that are currently awaiting verification.
            if (test.Status != "Completed")
            {
                TempData["Error"] =
                    "This test result is not currently waiting for verification.";

                return RedirectToAction(nameof(VerifyResults));
            }

            // Load the complete history for this specific test.
            // This includes the original result, previous reviews,
            // send-back notes and corrected results, if recorded.
        
ViewBag.TestResultHistory = await _context.TestResultHistories
    .Include(h => h.LabTechnician)
        .ThenInclude(t => t.User)
    .Where(h =>
        h.TestRequestTestTypeId == test.TestRequestTestTypeId)
    .OrderByDescending(h => h.ActionDateTime)
    .ToListAsync();


            // Display the result details and history.
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
    }
    }

using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Models.ViewModels;
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
            // Get the currently logged-in user
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }


            // Find the Lab Technician linked to the logged-in user
            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);


            // If there is no technician record yet,
            // show an empty dashboard
            if (technician == null)
            {
                return View(new LabTechnicianDashboardViewModel
                {
                    TechnicianName = user.FullName,
                    Workload = new List<LabTechnicianWorkloadViewModel>()
                });
            }


            // Tests assigned to this technician
            var myTests = _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .Where(t =>
                    t.LabTechnicianId == technician.LabTechnicianId);


            // Dashboard information
            var dashboard = new LabTechnicianDashboardViewModel
            {
                TechnicianName = user.FullName,


                // My selected tests
                MySelectedTests = await myTests
                    .CountAsync(t =>
                        t.Status != "Completed"),


                // Tests waiting to be selected
                WaitingToBeSelected = await _context.TestRequestTestTypes
                    .CountAsync(t =>
                        t.Status == "Submitted" &&
                        t.LabTechnicianId == null),


                // Tests waiting for verification
                WaitingVerification = await myTests
                    .CountAsync(t =>
                        t.Status == "Completed"),


                // Results waiting for review
                WaitingReview = await _context.TestResultVerifications
                    .CountAsync(v =>
                        v.Status == "To Be Reviewed"),


                // Urgent / STAT tests
                UrgentTests = await myTests
                    .CountAsync(t =>
                        t.TestRequest.Urgency == "STAT")
            };


            // TEST WORKLOAD
           

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


            // OVERDUE TESTS
          

            dashboard.OverdueTests = dashboard.Workload
                .Count(t =>
                    t.DueTime.HasValue &&
                    t.DueTime.Value < DateTime.Now &&
                    t.Status != "Completed");


            // ==========================================
            // NEARING TURNAROUND
            // ==========================================

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



        // ==========================================
        // CONFIRM RECEIVE SAMPLE
        // ==========================================

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

            // Mark this one sample as received
            sample.DateTimeReceived = DateTime.Now;
            sample.LabTechnicianId = technician.LabTechnicianId;

            // Check if every sample on this request has now been received
            bool allReceived = sample.TestRequest.Samples
                .All(s => s.DateTimeReceived.HasValue);

            if (allReceived)
            {
                sample.TestRequest.Status = "SamplesReceived";
            }

            await _context.SaveChangesAsync();

            TempData["SampleReceivedSuccess"] =
                allReceived
                    ? $"All samples for Request #{sample.TestRequestId} have been received."
                    : $"Sample {sample.Barcode} received. Waiting on remaining samples.";

            TempData["ReceivedRequestId"] = sample.TestRequestId;

            return RedirectToAction(nameof(ReceiveSampleDetails), new { id = sample.TestRequestId });
        }


        // PROCESS TESTS


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

            // Test types this technician is qualified to perform
            var qualifiedTestTypeIds = await _context.TechnicianTestTypes
                .Where(tt => tt.LabTechnicianId == technician.LabTechnicianId)
                .Select(tt => tt.TestTypeId)
                .ToListAsync();

            var query = _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                    .ThenInclude(tt => tt.Category)
                .Where(t =>
                    t.Status == "Submitted" &&
                    t.TestRequest.Status == "SamplesReceived" &&
                    qualifiedTestTypeIds.Contains(t.TestTypeId))
                .AsQueryable();

            // FILTERS
            if (!string.IsNullOrEmpty(urgency))
            {
                query = query.Where(t => t.TestRequest.Urgency == urgency);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(t => t.TestType.CategoryId == categoryId.Value);
            }

            if (requestNumber.HasValue)
            {
                query = query.Where(t => t.TestRequestId == requestNumber.Value);
            }

            if (dueDate.HasValue)
            {
                // "Due" here = request date + turnaround time, approximated at query time
                query = query.Where(t =>
                    t.TestRequest.RequestDate.AddMinutes(t.TestType.TurnAroundTime).Date == dueDate.Value.Date);
            }

            var tests = await query.ToListAsync();

            // Pass category list for the filter dropdown
            ViewBag.Categories = await _context.TestCategories
                .Where(c => c.IsActive)
                .ToListAsync();

            return View(tests);
        }



        // ==========================================
        // START PROCESSING TEST
        // ==========================================

        
[HttpPost]
public async Task<IActionResult> StartProcessing(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get the logged-in lab technician
            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            // Get the specific test request/test type
            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                .Include(t => t.TestType)
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == id);

            if (test == null)
            {
                return NotFound("Test could not be found.");
            }

            // The test must still be waiting to be selected
            if (test.Status != "Submitted")
            {
                TempData["Error"] = "This test has already been selected or processed.";
                return RedirectToAction(nameof(ProcessTests));
            }

            // The request must have received samples before processing can start
            if (test.TestRequest.Status != "SamplesReceived")
            {
                TempData["Error"] =
                    "This test cannot be started because the required sample has not been received.";

                return RedirectToAction(nameof(ProcessTests));
            }

            // Check that the technician is allowed to perform this test type.
            // This assignment is normally made by the Laboratory Manager.
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

            // Check whether this is the first test being selected
            // for this test request.
            bool firstTestForRequest = !await _context.TestRequestTestTypes
                .AnyAsync(t =>
                    t.TestRequestId == test.TestRequestId &&
                    t.Status == "InProgress");

            // Assign the specific test to the logged-in technician
            test.LabTechnicianId = technician.LabTechnicianId;

            // Change the test status to In Progress
            test.Status = "InProgress";

            // Record when the technician started the test
            test.StartDateTime = DateTime.Now;

            // If this is the first test selected from the request,
            // the overall request becomes In Progress.
            if (firstTestForRequest)
            {
                test.TestRequest.Status = "InProgress";
            }

            await _context.SaveChangesAsync();

            // Take the technician to the result capture screen
            return RedirectToAction(
                nameof(CaptureTestResult),
                new { id = test.TestRequestTestTypeId });
        }


        // ==========================================
        // CAPTURE TEST RESULT
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> CaptureTestResult(int id)
        {
            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)

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


            // ==========================================
            // GET PATIENT CONDITIONS
            // ==========================================

            var patientId = test.TestRequest.Patient.PatientID;


                        var conditions = await _context.PatientConditions
                            .Include(x => x.Condition)
                            .Where(x => x.PatientID == patientId)
                            .ToListAsync();


            // ==========================================
            // GET PATIENT ALLERGIES
            // ==========================================

            var allergies = await _context.PatientAllergies
                .Include(x => x.Allergy)
                .Where(x => x.PatientID == patientId)
                .ToListAsync();


            // ==========================================
            // GET PATIENT MEDICATIONS
            // ==========================================

            var medications = await _context.PatientMedications
                .Include(x => x.Medication)
                .Where(x => x.PatientID == patientId)
                .ToListAsync();


            // Make these available to the view
            ViewBag.PatientConditions = conditions;
            ViewBag.PatientAllergies = allergies;
            ViewBag.PatientMedications = medications;


            return View(test);
        }
        // ==========================================
        // SAVE TEST RESULT
        // ==========================================

      
// ==========================================
// SAVE TEST RESULT
// ==========================================

[HttpPost]
public async Task<IActionResult> SaveTestResult(
    int testRequestTestTypeId,
    string resultValue,
    string notes)
        {
            // Get the test, including its Test Type
            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestType)
                .Include(t => t.TestRequest)
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == testRequestTestTypeId);

            if (test == null)
            {
                return NotFound();
            }

            // Make sure the result is not empty
            if (string.IsNullOrWhiteSpace(resultValue))
            {
                TempData["Error"] = "Please enter a result value.";

                return RedirectToAction(
                    nameof(CaptureTestResult),
                    new { id = testRequestTestTypeId });
            }

            // Try to convert the result into a number
            if (!double.TryParse(
                resultValue,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double numericResult))
            {
                TempData["Error"] =
                    "Please enter a valid numeric result.";

                return RedirectToAction(
                    nameof(CaptureTestResult),
                    new { id = testRequestTestTypeId });
            }

            // Get the normal range configured for this test type
            double normalMin = test.TestType.NormalRangeMin;
            double normalMax = test.TestType.NormalRangeMax;

            // Automatically determine whether the result is abnormal
            bool isAbnormal =
                numericResult < normalMin ||
                numericResult > normalMax;

            // Create the test result
            var testResult = new TestResult
            {
                ResultValue = resultValue,
                Notes = notes,
                IsAbnormal = isAbnormal,
                DateCaptured = DateTime.Now
            };

            _context.TestResults.Add(testResult);

            await _context.SaveChangesAsync();

            // Link the result to the test
            test.TestResultId = testResult.TestResultId;

            // Mark the test as completed
            test.Status = "Completed";

            // Record completion time
            test.CompletionDateTime = DateTime.Now;

            // Check whether this was the last outstanding test
            bool allTestsCompleted = !await _context.TestRequestTestTypes
                .AnyAsync(t =>
                    t.TestRequestId == test.TestRequestId &&
                    t.TestRequestTestTypeId != test.TestRequestTestTypeId &&
                    t.Status != "Completed");

            // If all tests are completed, update the request status
            if (allTestsCompleted)
            {
                test.TestRequest.Status = "Completed";
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ProcessTests));
        }



        // ==========================================
        // VERIFY TEST RESULTS
        // ==========================================

        public async Task<IActionResult> VerifyResults()
        {
            // Get completed tests that have a test result
            var testRequestTestTypes = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .Include(t => t.TestResult)
                .Where(t =>
                    t.Status == "Completed" &&
                    t.TestResultId != null)
                .ToListAsync();

            // Extract the TestResult objects
            var results = testRequestTestTypes
                .Where(t => t.TestResult != null)
                .Select(t => t.TestResult)
                .Distinct()
                .ToList();

            // Populate the existing TestResult field
            foreach (var result in results)
            {
                result.TestRequestTestType =
                    testRequestTestTypes
                        .Where(t => t.TestResultId == result.TestResultId)
                        .ToList();
            }

            return View(results);
        }


        // ==========================================
        // VERIFY RESULT - DETAILS
        // ==========================================

        public async Task<IActionResult> VerifyResultDetails(int id)
        {
            var result = await _context.TestResults
                .Include(r => r.TestRequestTestType)
                    .ThenInclude(t => t.TestRequest)
                        .ThenInclude(r => r.Patient)
                .Include(r => r.TestRequestTestType)
                    .ThenInclude(t => t.TestType)
                .FirstOrDefaultAsync(r =>
                    r.TestResultId == id);


            if (result == null)
            {
                return NotFound();
            }


            return View(result);
        }



        // ==========================================
        // VERIFY / SEND BACK TEST RESULT
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> VerifyResult(
            int id,
            string action,
            string reviewNotes)
        {
            var user = await _userManager.GetUserAsync(User);


            if (user == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }


            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t =>
                    t.UserId == user.Id);


            if (technician == null)
            {
                return NotFound(
                    "Lab technician profile not found.");
            }


            var result = await _context.TestResults
                .FirstOrDefaultAsync(r =>
                    r.TestResultId == id);


            if (result == null)
            {
                return NotFound();
            }


            var verification =
                new TestResultVerification
                {
                    TestResultId =
                        result.TestResultId,

                    VerifiedByTechnicianId =
                        technician.LabTechnicianId,

                    VerificationDateTime =
                        DateTime.Now,

                    ReviewNotes =
                        reviewNotes
                };


            if (action == "Verify")
            {
                verification.Status = "Verified";
            }
            else if (action == "SendBack")
            {
                verification.Status = "To Be Reviewed";
            }
            else
            {
                return BadRequest();
            }


            _context.TestResultVerifications
                .Add(verification);


            await _context.SaveChangesAsync();


            return RedirectToAction(
                nameof(VerifyResults));
        }



        // ==========================================
        // REVIEW TEST RESULTS
        // ==========================================

        public async Task<IActionResult> ReviewResults()
        {
            // Get results that have been sent back for review
            var verifications = await _context.TestResultVerifications
                .Include(v => v.TestResult)
                .Where(v => v.Status == "To Be Reviewed")
                .ToListAsync();

            // Load the related TestRequestTestType information
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
                                t.TestResultId == verification.TestResult.TestResultId)
                            .ToListAsync();
                }
            }

            return View(verifications);
        }


        // ==========================================
        // REVIEW RESULT - DETAILS
        // ==========================================

        public async Task<IActionResult> ReviewResultDetails(int id)
        {
            var verification = await _context.TestResultVerifications
                .Include(v => v.TestResult)
                .FirstOrDefaultAsync(v =>
                    v.TestResultVerificationId == id);

            if (verification == null)
            {
                return NotFound();
            }

            if (verification.TestResult != null)
            {
                verification.TestResult.TestRequestTestType =
                    await _context.TestRequestTestTypes
                        .Include(t => t.TestRequest)
                            .ThenInclude(r => r.Patient)
                        .Include(t => t.TestType)
                        .Where(t =>
                            t.TestResultId == verification.TestResult.TestResultId)
                        .ToListAsync();
            }

            return View(verification);
        }

        // ==========================================
        // COMPLETE REVIEW
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> CompleteReview(
            int id,
            string action,
            string reviewNotes)
        {
            var verification =
                await _context.TestResultVerifications
                    .FirstOrDefaultAsync(v =>
                        v.TestResultVerificationId == id);


            if (verification == null)
            {
                return NotFound();
            }


            verification.ReviewNotes =
                reviewNotes;


            verification.VerificationDateTime =
                DateTime.Now;


            if (action == "Verify")
            {
                verification.Status = "Verified";
            }
            else if (action == "SendBack")
            {
                verification.Status = "To Be Reviewed";
            }
            else
            {
                return BadRequest();
            }


            await _context.SaveChangesAsync();


            return RedirectToAction(
                nameof(ReviewResults));
        }
        public async Task<IActionResult> Reports(
     DateTime? fromDate,
     DateTime? toDate,
     string testType,
     string status,
     string urgency)
        {
            var query = _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .Include(t => t.TestResult)
                .Include(t => t.LabTechnician)
                .AsQueryable();


            // DATE FILTER
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


            // TEST TYPE FILTER
            if (!string.IsNullOrEmpty(testType))
            {
                query = query.Where(t =>
                    t.TestType.TestTypeName == testType);
            }


            // STATUS FILTER
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t =>
                    t.Status == status);
            }


            // URGENCY FILTER
            if (!string.IsNullOrEmpty(urgency))
            {
                query = query.Where(t =>
                    t.TestRequest.Urgency == urgency);
            }


            var tests = await query.ToListAsync();


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
                    t.TestResult != null &&
                    t.TestResult.IsAbnormal)
            };


            report.Tests = tests.Select(t =>
                new LabTechnicianReportItemViewModel
                {
                    TestRequestId = t.TestRequestId,

                    PatientName =
                        (t.TestRequest?.Patient?.Name ?? "") +
                        " " +
                        (t.TestRequest?.Patient?.Surname ?? ""),

                    TestTypeName =
                        t.TestType?.TestTypeName ?? "N/A",

                    TechnicianName =
                        (t.LabTechnician?.Name ?? "") +
                        " " +
                        (t.LabTechnician?.Surname ?? ""),

                    Urgency =
                        t.TestRequest?.Urgency ?? "N/A",

                    Status =
                        t.Status ?? "N/A",

                    ResultValue =
                        t.TestResult?.ResultValue ?? "N/A",

                    IsAbnormal =
                        t.TestResult?.IsAbnormal ?? false,

                    StartDateTime =
                        t.StartDateTime,

                    CompletionDateTime =
                        t.CompletionDateTime,

                    TurnaroundMinutes =
                        t.StartDateTime.HasValue &&
                        t.CompletionDateTime.HasValue
                            ? (t.CompletionDateTime.Value -
                               t.StartDateTime.Value).TotalMinutes
                            : null
                })
                .ToList();


            return View(report);
        }
        public async Task<IActionResult> ExportReportPdf(
    DateTime? fromDate,
    DateTime? toDate,
    string testType,
    string status,
    string urgency)
        {
            var query = _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .Include(t => t.TestResult)
                .Include(t => t.LabTechnician)
                .AsQueryable();


            // DATE FILTER
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


            // TEST TYPE FILTER
            if (!string.IsNullOrEmpty(testType))
            {
                query = query.Where(t =>
                    t.TestType.TestTypeName == testType);
            }


            // STATUS FILTER
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t =>
                    t.Status == status);
            }


            // URGENCY FILTER
            if (!string.IsNullOrEmpty(urgency))
            {
                query = query.Where(t =>
                    t.TestRequest.Urgency == urgency);
            }


            var tests = await query.ToListAsync();


            var reportTests = tests.Select(t =>
                new LabTechnicianReportItemViewModel
                {
                    TestRequestId = t.TestRequestId,

                    PatientName =
                        (t.TestRequest?.Patient?.Name ?? "") +
                        " " +
                        (t.TestRequest?.Patient?.Surname ?? ""),

                    TestTypeName =
                        t.TestType?.TestTypeName ?? "N/A",

                    TechnicianName =
                        (t.LabTechnician?.Name ?? "") +
                        " " +
                        (t.LabTechnician?.Surname ?? ""),

                    Urgency =
                        t.TestRequest?.Urgency ?? "N/A",

                    Status =
                        t.Status ?? "N/A",

                    ResultValue =
                        t.TestResult?.ResultValue ?? "N/A",

                    IsAbnormal =
                        t.TestResult?.IsAbnormal ?? false,

                    StartDateTime =
                        t.StartDateTime,

                    CompletionDateTime =
                        t.CompletionDateTime,

                    TurnaroundMinutes =
                        t.StartDateTime.HasValue &&
                        t.CompletionDateTime.HasValue
                            ? (t.CompletionDateTime.Value -
                               t.StartDateTime.Value).TotalMinutes
                            : null
                })
                .ToList();


            var totalTests = reportTests.Count;

            var completedTests = reportTests.Count(t =>
                t.Status == "Completed");

            var inProgressTests = reportTests.Count(t =>
                t.Status == "InProgress");

            var abnormalResults = reportTests.Count(t =>
                t.IsAbnormal);


            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);

                    page.Header()
                        .Text("HEMACORE")
                        .FontSize(20)
                        .Bold();

                    page.Content().Column(column =>
                    {
                        column.Spacing(10);

                        column.Item()
                            .Text("LAB TECHNICIAN TEST PROCESSING REPORT")
                            .FontSize(16)
                            .Bold();

                        column.Item()
                            .Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}");

                        column.Item()
                            .Text(
                                $"Date Range: " +
                                $"{(fromDate.HasValue ? fromDate.Value.ToString("dd/MM/yyyy") : "All")} - " +
                                $"{(toDate.HasValue ? toDate.Value.ToString("dd/MM/yyyy") : "All")}"
                            );


                        // SUMMARY
                        column.Item()
                            .PaddingTop(10)
                            .Text("REPORT SUMMARY")
                            .FontSize(13)
                            .Bold();


                        column.Item().Text(
                            $"Total Tests: {totalTests}");

                        column.Item().Text(
                            $"Completed Tests: {completedTests}");

                        column.Item().Text(
                            $"In Progress: {inProgressTests}");

                        column.Item().Text(
                            $"Abnormal Results: {abnormalResults}");


                        // FILTERS
                        column.Item()
                            .PaddingTop(10)
                            .Text("FILTERS APPLIED")
                            .FontSize(13)
                            .Bold();

                        column.Item().Text(
                            $"Test Type: {(string.IsNullOrEmpty(testType) ? "All" : testType)}");

                        column.Item().Text(
                            $"Status: {(string.IsNullOrEmpty(status) ? "All" : status)}");

                        column.Item().Text(
                            $"Urgency: {(string.IsNullOrEmpty(urgency) ? "All" : urgency)}");


                        // TABLE
                        column.Item()
                            .PaddingTop(15)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
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
                                    header.Cell().Text("Request").Bold();
                                    header.Cell().Text("Patient").Bold();
                                    header.Cell().Text("Test").Bold();
                                    header.Cell().Text("Technician").Bold();
                                    header.Cell().Text("Urgency").Bold();
                                    header.Cell().Text("Status").Bold();
                                    header.Cell().Text("Result").Bold();
                                });


                                foreach (var test in reportTests)
                                {
                                    table.Cell()
                                        .Text($"#{test.TestRequestId}");

                                    table.Cell()
                                        .Text(test.PatientName);

                                    table.Cell()
                                        .Text(test.TestTypeName);

                                    table.Cell()
                                        .Text(test.TechnicianName);

                                    table.Cell()
                                        .Text(test.Urgency);

                                    table.Cell()
                                        .Text(test.Status);

                                    table.Cell()
                                        .Text(test.ResultValue);
                                }
                            });
                    });


                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("HemaCore - Lab Technician Report | ");
                            text.CurrentPageNumber();
                        });
                });
            });


            var pdfBytes = document.GeneratePdf();

            return File(
                pdfBytes,
                "application/pdf",
                $"LabTechnicianReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }
    }
}
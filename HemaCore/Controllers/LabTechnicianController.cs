
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

                MySelectedTests = await myTests
                    .CountAsync(t =>
                        t.Status != "Completed"),

                WaitingToBeSelected = await _context.TestRequestTestTypes
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
                .FirstOrDefaultAsync(s =>
                    s.SampleId == sampleId);

            if (sample == null)
            {
                return NotFound();
            }

            sample.DateTimeReceived = DateTime.Now;
            sample.LabTechnicianId =
                technician.LabTechnicianId;

            bool allReceived = sample.TestRequest.Samples
                .All(s => s.DateTimeReceived.HasValue);

            if (allReceived)
            {
                sample.TestRequest.Status =
                    "SamplesReceived";
            }

            await _context.SaveChangesAsync();

            TempData["SampleReceivedSuccess"] =
                allReceived
                    ? $"All samples for Request #{sample.TestRequestId} have been received."
                    : $"Sample {sample.Barcode} received. Waiting on remaining samples.";

            TempData["ReceivedRequestId"] =
                sample.TestRequestId;

            return RedirectToAction(
                nameof(ReceiveSampleDetails),
                new
                {
                    id = sample.TestRequestId
                });
        }


        // ==========================================
        // PROCESS TESTS
        // ==========================================

        public async Task<IActionResult> ProcessTests(
            string urgency,
            int? categoryId,
            int? requestNumber,
            DateTime? dueDate)
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

            var qualifiedTestTypeIds =
                await _context.TechnicianTestTypes
                    .Where(tt =>
                        tt.LabTechnicianId ==
                        technician.LabTechnicianId)
                    .Select(tt =>
                        tt.TestTypeId)
                    .ToListAsync();

            var query =
                _context.TestRequestTestTypes
                    .Include(t => t.TestRequest)
                        .ThenInclude(r => r.Patient)
                    .Include(t => t.TestType)
                        .ThenInclude(tt => tt.Category)
                    .Where(t =>
                        t.Status == "Submitted" &&
                        t.TestRequest.Status ==
                            "SamplesReceived" &&
                        qualifiedTestTypeIds.Contains(
                            t.TestTypeId))
                    .AsQueryable();

            if (!string.IsNullOrEmpty(urgency))
            {
                query = query.Where(t =>
                    t.TestRequest.Urgency == urgency);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(t =>
                    t.TestType.CategoryId ==
                    categoryId.Value);
            }

            if (requestNumber.HasValue)
            {
                query = query.Where(t =>
                    t.TestRequestId ==
                    requestNumber.Value);
            }

            if (dueDate.HasValue)
            {
                query = query.Where(t =>
                    t.TestRequest.RequestDate
                        .AddMinutes(
                            t.TestType.TurnAroundTime)
                        .Date ==
                    dueDate.Value.Date);
            }

            var tests =
                await query.ToListAsync();

            ViewBag.Categories =
                await _context.TestCategories
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

            var test =
                await _context.TestRequestTestTypes
                    .Include(t => t.TestRequest)
                    .Include(t => t.TestType)
                    .FirstOrDefaultAsync(t =>
                        t.TestRequestTestTypeId == id);

            if (test == null)
            {
                return NotFound(
                    "Test could not be found.");
            }

            if (test.Status != "Submitted")
            {
                TempData["Error"] =
                    "This test has already been selected or processed.";

                return RedirectToAction(
                    nameof(ProcessTests));
            }

            if (test.TestRequest.Status !=
                "SamplesReceived")
            {
                TempData["Error"] =
                    "This test cannot be started because the required sample has not been received.";

                return RedirectToAction(
                    nameof(ProcessTests));
            }

            var isAssignedTestType =
                await _context.TechnicianTestTypes
                    .AnyAsync(tt =>
                        tt.LabTechnicianId ==
                            technician.LabTechnicianId &&
                        tt.TestTypeId ==
                            test.TestTypeId);

            if (!isAssignedTestType)
            {
                TempData["Error"] =
                    "You are not assigned to perform this test type.";

                return RedirectToAction(
                    nameof(ProcessTests));
            }

            bool firstTestForRequest =
                !await _context.TestRequestTestTypes
                    .AnyAsync(t =>
                        t.TestRequestId ==
                            test.TestRequestId &&
                        t.Status == "InProgress");

            test.LabTechnicianId =
                technician.LabTechnicianId;

            test.Status =
                "InProgress";

            test.StartDateTime =
                DateTime.Now;

            if (firstTestForRequest)
            {
                test.TestRequest.Status =
                    "InProgress";
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(CaptureTestResult),
                new
                {
                    id =
                        test.TestRequestTestTypeId
                });
        }


        // ==========================================
        // CAPTURE TEST RESULT
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> CaptureTestResult(int id)
        {
            var test =
                await _context.TestRequestTestTypes
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

            var patientId =
                test.TestRequest.Patient.PatientID;

            var conditions =
                await _context.PatientConditions
                    .Include(x => x.Condition)
                    .Where(x =>
                        x.PatientID == patientId)
                    .ToListAsync();

            var allergies =
                await _context.PatientAllergies
                    .Include(x => x.Allergy)
                    .Where(x =>
                        x.PatientID == patientId)
                    .ToListAsync();

            var medications =
                await _context.PatientMedications
                    .Include(x => x.Medication)
                    .Where(x =>
                        x.PatientID == patientId)
                    .ToListAsync();

            ViewBag.PatientConditions =
                conditions;

            ViewBag.PatientAllergies =
                allergies;

            ViewBag.PatientMedications =
                medications;

            return View(test);
        }


        // ==========================================
        // SAVE TEST RESULT
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> SaveTestResult(
            int testRequestTestTypeId,
            string resultValue,
            string notes)
        {
            var test =
                await _context.TestRequestTestTypes
                    .Include(t => t.TestType)
                    .Include(t => t.TestRequest)
                    .FirstOrDefaultAsync(t =>
                        t.TestRequestTestTypeId ==
                        testRequestTestTypeId);

            if (test == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(
                resultValue))
            {
                TempData["Error"] =
                    "Please enter a result value.";

                return RedirectToAction(
                    nameof(CaptureTestResult),
                    new
                    {
                        id =
                            testRequestTestTypeId
                    });
            }

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
                    new
                    {
                        id =
                            testRequestTestTypeId
                    });
            }

            double normalMin =
                test.TestType.NormalRangeMin;

            double normalMax =
                test.TestType.NormalRangeMax;

            bool isAbnormal =
                numericResult < normalMin ||
                numericResult > normalMax;

            var testResult =
                new TestResult
                {
                    ResultValue =
                        resultValue,

                    Notes =
                        notes,

                    IsAbnormal =
                        isAbnormal,

                    DateCaptured =
                        DateTime.Now
                };

            _context.TestResults.Add(
                testResult);

            await _context.SaveChangesAsync();

            test.TestResultId =
                testResult.TestResultId;

            test.Status =
                "Completed";

            test.CompletionDateTime =
                DateTime.Now;

            bool allTestsCompleted =
                !await _context.TestRequestTestTypes
                    .AnyAsync(t =>
                        t.TestRequestId ==
                            test.TestRequestId &&
                        t.TestRequestTestTypeId !=
                            test.TestRequestTestTypeId &&
                        t.Status != "Completed");

            if (allTestsCompleted)
            {
                test.TestRequest.Status =
                    "Completed";
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(ProcessTests));
        }


        // ==========================================
        // VERIFY TEST RESULTS - LIST
        // ==========================================

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


        // ==========================================
        // VERIFY RESULT - DETAILS
        // ==========================================

        public async Task<IActionResult> VerifyResultDetails(
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

            var result =
                await _context.TestResults
                    .Include(r =>
                        r.TestRequestTestType)
                        .ThenInclude(t =>
                            t.TestRequest)
                            .ThenInclude(r =>
                                r.Patient)

                    .Include(r =>
                        r.TestRequestTestType)
                        .ThenInclude(t =>
                            t.TestType)

                    .FirstOrDefaultAsync(r =>
                        r.TestResultId == id);

            if (result == null)
            {
                return NotFound(
                    "Test result could not be found.");
            }

            var test =
                result.TestRequestTestType
                    .FirstOrDefault();

            if (test == null)
            {
                return NotFound(
                    "The test associated with this result could not be found.");
            }

            if (test.LabTechnicianId ==
                technician.LabTechnicianId)
            {
                TempData["Error"] =
                    "You cannot verify a result that you performed yourself.";

                return RedirectToAction(
                    nameof(VerifyResults));
            }

            var canVerify =
                await _context.TechnicianTestTypes
                    .AnyAsync(tt =>
                        tt.LabTechnicianId ==
                            technician.LabTechnicianId &&
                        tt.TestTypeId ==
                            test.TestTypeId);

            if (!canVerify)
            {
                TempData["Error"] =
                    "You are not assigned to verify this test type.";

                return RedirectToAction(
                    nameof(VerifyResults));
            }

            if (test.Status != "Completed")
            {
                TempData["Error"] =
                    "This test result is not currently waiting for verification.";

                return RedirectToAction(
                    nameof(VerifyResults));
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

            var result =
                await _context.TestResults
                    .Include(r =>
                        r.TestRequestTestType)
                    .FirstOrDefaultAsync(r =>
                        r.TestResultId == id);

            if (result == null)
            {
                return NotFound(
                    "Test result could not be found.");
            }

            var test =
                result.TestRequestTestType
                    .FirstOrDefault();

            if (test == null)
            {
                return NotFound(
                    "The test associated with this result could not be found.");
            }


            // ==========================================
            // PREVENT SELF-VERIFICATION
            // ==========================================

            if (test.LabTechnicianId ==
                technician.LabTechnicianId)
            {
                TempData["Error"] =
                    "You cannot verify a result that you performed yourself.";

                return RedirectToAction(
                    nameof(VerifyResults));
            }


            // ==========================================
            // CHECK TECHNICIAN PERMISSION
            // ==========================================

            var canVerify =
                await _context.TechnicianTestTypes
                    .AnyAsync(tt =>
                        tt.LabTechnicianId ==
                            technician.LabTechnicianId &&
                        tt.TestTypeId ==
                            test.TestTypeId);

            if (!canVerify)
            {
                TempData["Error"] =
                    "You are not assigned to verify this test type.";

                return RedirectToAction(
                    nameof(VerifyResults));
            }


            // ==========================================
            // RESULT MUST BE COMPLETED
            // ==========================================

            if (test.Status != "Completed")
            {
                TempData["Error"] =
                    "This test result is not currently waiting for verification.";

                return RedirectToAction(
                    nameof(VerifyResults));
            }


            // ==========================================
            // FIND EXISTING VERIFICATION
            // ==========================================

            var verification =
                await _context.TestResultVerifications
                    .FirstOrDefaultAsync(v =>
                        v.TestResultId ==
                        result.TestResultId);


            // ==========================================
            // VERIFY RESULT
            // ==========================================

            if (action == "Verify")
            {
                test.Status =
                    "Verified";

                if (verification == null)
                {
                    verification =
                        new TestResultVerification
                        {
                            TestResultId =
                                result.TestResultId,

                            VerifiedByTechnicianId =
                                technician.LabTechnicianId,

                            VerificationDateTime =
                                DateTime.Now,

                            Status =
                                "Verified",

                            ReviewNotes =
                                reviewNotes ?? ""
                        };

                    _context.TestResultVerifications
                        .Add(verification);
                }
                else
                {
                    verification.VerifiedByTechnicianId =
                        technician.LabTechnicianId;

                    verification.VerificationDateTime =
                        DateTime.Now;

                    verification.Status =
                        "Verified";

                    verification.ReviewNotes =
                        reviewNotes ?? "";
                }

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Test result has been successfully verified.";

                return RedirectToAction(
                    nameof(VerifyResults));
            }


            // ==========================================
            // SEND BACK FOR REVIEW
            // ==========================================

            if (action == "SendBack")
            {
                if (string.IsNullOrWhiteSpace(
                    reviewNotes))
                {
                    TempData["Error"] =
                        "Please provide a reason before sending the result back for review.";

                    return RedirectToAction(
                        nameof(VerifyResultDetails),
                        new
                        {
                            id =
                                result.TestResultId
                        });
                }

                test.Status =
                    "To Be Reviewed";

                if (verification == null)
                {
                    verification =
                        new TestResultVerification
                        {
                            TestResultId =
                                result.TestResultId,

                            VerifiedByTechnicianId =
                                technician.LabTechnicianId,

                            VerificationDateTime =
                                DateTime.Now,

                            Status =
                                "To Be Reviewed",

                            ReviewNotes =
                                reviewNotes
                        };

                    _context.TestResultVerifications
                        .Add(verification);
                }
                else
                {
                    verification.VerifiedByTechnicianId =
                        technician.LabTechnicianId;

                    verification.VerificationDateTime =
                        DateTime.Now;

                    verification.Status =
                        "To Be Reviewed";

                    verification.ReviewNotes =
                        reviewNotes;
                }

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Test result has been sent back to the original technician for review.";

                return RedirectToAction(
                    nameof(VerifyResults));
            }

            return BadRequest(
                "Invalid verification action.");
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


        // ==========================================
        // COMPLETE REVIEW / RESUBMIT
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> CompleteReview(
            int id,
            string resultValue,
            string notes)
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
                            id);

            if (verification == null)
            {
                TempData["Error"] =
                    "The review record could not be found.";

                return RedirectToAction(
                    nameof(ReviewResults));
            }

            if (verification.TestResult == null)
            {
                TempData["Error"] =
                    "The test result could not be found.";

                return RedirectToAction(
                    nameof(ReviewResults));
            }

            var result =
                verification.TestResult;

            var test =
                await _context
                    .TestRequestTestTypes
                    .Include(t =>
                        t.TestType)
                    .Include(t =>
                        t.TestRequest)
                    .FirstOrDefaultAsync(t =>
                        t.TestResultId ==
                            result.TestResultId);

            if (test == null)
            {
                TempData["Error"] =
                    "The test associated with this result could not be found.";

                return RedirectToAction(
                    nameof(ReviewResults));
            }


            // ==========================================
            // ONLY ORIGINAL TECHNICIAN CAN REVIEW
            // ==========================================

            if (test.LabTechnicianId !=
                technician.LabTechnicianId)
            {
                TempData["Error"] =
                    "You are not authorised to review this test result.";

                return RedirectToAction(
                    nameof(ReviewResults));
            }


            // ==========================================
            // MUST BE WAITING FOR REVIEW
            // ==========================================

            if (test.Status !=
                "To Be Reviewed")
            {
                TempData["Error"] =
                    "This test result is no longer waiting for review.";

                return RedirectToAction(
                    nameof(ReviewResults));
            }


            // ==========================================
            // VALIDATE RESULT
            // ==========================================

            if (string.IsNullOrWhiteSpace(
                resultValue))
            {
                TempData["Error"] =
                    "Please enter a result value.";

                return RedirectToAction(
                    nameof(ReviewResultDetails),
                    new
                    {
                        id =
                            verification
                                .TestResultVerificationId
                    });
            }


            if (!double.TryParse(
                resultValue,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double numericResult))
            {
                TempData["Error"] =
                    "Please enter a valid numeric result.";

                return RedirectToAction(
                    nameof(ReviewResultDetails),
                    new
                    {
                        id =
                            verification
                                .TestResultVerificationId
                    });
            }


            // ==========================================
            // RECALCULATE ABNORMAL STATUS
            // ==========================================

            double normalMin =
                test.TestType.NormalRangeMin;

            double normalMax =
                test.TestType.NormalRangeMax;

            bool isAbnormal =
                numericResult < normalMin ||
                numericResult > normalMax;


            // ==========================================
            // UPDATE EXISTING RESULT
            // ==========================================

            result.ResultValue =
                resultValue;

            result.Notes =
                notes ?? "";

            result.IsAbnormal =
                isAbnormal;

            result.DateCaptured =
                DateTime.Now;


            // ==========================================
            // RESUBMIT FOR VERIFICATION
            // ==========================================

            test.Status =
                "Completed";

            test.CompletionDateTime =
                DateTime.Now;


            // Reuse the existing verification record.
            // Do NOT create another one because
            // TestResultId is unique.
            verification.Status =
                "Resubmitted";

            verification.VerificationDateTime =
                DateTime.Now;


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "The corrected result has been resubmitted for verification.";

            return RedirectToAction(
                nameof(ReviewResults));
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

            var report =
                new LabTechnicianReportViewModel
                {
                    FromDate =
                        fromDate,

                    ToDate =
                        toDate,

                    TestType =
                        testType,

                    Status =
                        status,

                    Urgency =
                        urgency,

                    TotalTests =
                        tests.Count,

                    CompletedTests =
                        tests.Count(t =>
                            t.Status ==
                            "Completed"),

                    InProgressTests =
                        tests.Count(t =>
                            t.Status ==
                            "InProgress"),

                    AbnormalResults =
                        tests.Count(t =>
                            t.TestResult != null &&
                            t.TestResult.IsAbnormal)
                };

            report.Tests =
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

            var completedTests =
                await _context.TestRequestTestTypes
                    .Include(t =>
                        t.TestRequest)
                        .ThenInclude(r =>
                            r.Patient)
                    .Include(t =>
                        t.TestType)
                    .Where(t =>
                        t.LabTechnicianId ==
                            technician.LabTechnicianId &&
                        (
                            t.Status ==
                                "Completed" ||
                            t.Status ==
                                "Verified"
                        ))
                    .OrderByDescending(t =>
                        t.CompletionDateTime)
                    .ToListAsync();

            return View(completedTests);
        }
    }
}

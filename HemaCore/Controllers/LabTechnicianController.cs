using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemaCore.Controllers
{
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
        // TECHNICIAN DASHBOARD
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
                    TechnicianName = user.FullName
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



        // RECEIVE SAMPLE


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



        // RECEIVE SAMPLES - DETAILS


        public async Task<IActionResult> ReceiveSampleDetails(int id)
        {
            var testRequest = await _context.TestRequests
                .Include(t => t.Patient)
                .Include(t => t.Doctor)
                .Include(t => t.Samples)
                    .ThenInclude(s => s.SampleType)
                .FirstOrDefaultAsync(t => t.TestRequestId == id);

            if (testRequest == null)
            {
                return NotFound();
            }

            return View(testRequest);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmReceiveSample(int id)
        {
            var testRequest = await _context.TestRequests
                .Include(t => t.Samples)
                .FirstOrDefaultAsync(t => t.TestRequestId == id);

            if (testRequest == null)
            {
                return NotFound();
            }

            foreach (var sample in testRequest.Samples)
            {
                sample.DateTimeReceived = DateTime.Now;
            }

            testRequest.Status = "SamplesReceived";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ReceiveSample));
        }
        public async Task<IActionResult> ProcessTests()
        {
            var tests = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .Where(t =>
                    t.Status == "Submitted" &&
                    t.TestRequest.Status == "SamplesReceived")
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
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == id);

            if (test == null)
            {
                return NotFound();
            }

            test.LabTechnicianId = technician.LabTechnicianId;

            test.Status = "InProgress";

            test.StartDateTime = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(CaptureTestResult),
                new { id = test.TestRequestTestTypeId }
            );
        }
        [HttpGet]
        public async Task<IActionResult> CaptureTestResult(int id)
        {
            var test = await _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == id);

            if (test == null)
            {
                return NotFound();
            }

            return View(test);
        }
    
// ==========================================
// SAVE TEST RESULT
// ==========================================

[HttpPost]
        public async Task<IActionResult> SaveTestResult(
    int testRequestTestTypeId,
    string resultValue,
    string notes,
    bool isAbnormal)
        {
            var test = await _context.TestRequestTestTypes
                .FirstOrDefaultAsync(t =>
                    t.TestRequestTestTypeId == testRequestTestTypeId);

            if (test == null)
            {
                return NotFound();
            }

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

            test.CompletionDateTime = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ProcessTests));
        }
       
// ==========================================
// VERIFY TEST RESULTS
// ==========================================

public async Task<IActionResult> VerifyResults()
        {
            var results = await _context.TestResults
                .Include(r => r.TestRequestTestType)
                    .ThenInclude(t => t.TestRequest)
                        .ThenInclude(r => r.Patient)
                .Include(r => r.TestRequestTestType)
                    .ThenInclude(t => t.TestType)
                .Where(r =>
                    r.TestRequestTestType.Any(t =>
                        t.Status == "Completed"))
                .ToListAsync();

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
                return RedirectToAction("Login", "Account");
            }

            var technician = await _context.LabTechnicians
                .FirstOrDefaultAsync(t => t.UserId == user.Id);

            if (technician == null)
            {
                return NotFound("Lab technician profile not found.");
            }

            var result = await _context.TestResults
                .FirstOrDefaultAsync(r => r.TestResultId == id);

            if (result == null)
            {
                return NotFound();
            }

            var verification = new TestResultVerification
            {
                TestResultId = result.TestResultId,
                VerifiedByTechnicianId = technician.LabTechnicianId,
                VerificationDateTime = DateTime.Now,
                ReviewNotes = reviewNotes
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

            _context.TestResultVerifications.Add(verification);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(VerifyResults));
        }


     
// ==========================================
// REVIEW TEST RESULTS
// ==========================================

public async Task<IActionResult> ReviewResults()
        {
            var results = await _context.TestResultVerifications
                .Include(v => v.TestResult)
                    .ThenInclude(r => r.TestRequestTestType)
                        .ThenInclude(t => t.TestRequest)
                            .ThenInclude(r => r.Patient)
                .Include(v => v.TestResult)
                    .ThenInclude(r => r.TestRequestTestType)
                        .ThenInclude(t => t.TestType)
                .Where(v => v.Status == "To Be Reviewed")
                .ToListAsync();

            return View(results);
        }

        
// ==========================================
// REVIEW RESULT - DETAILS
// ==========================================

public async Task<IActionResult> ReviewResultDetails(int id)
        {
            var verification = await _context.TestResultVerifications
                .Include(v => v.TestResult)
                    .ThenInclude(r => r.TestRequestTestType)
                        .ThenInclude(t => t.TestRequest)
                            .ThenInclude(r => r.Patient)
                .Include(v => v.TestResult)
                    .ThenInclude(r => r.TestRequestTestType)
                        .ThenInclude(t => t.TestType)
                .FirstOrDefaultAsync(v =>
                    v.TestResultVerificationId == id);

            if (verification == null)
            {
                return NotFound();
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
            var verification = await _context.TestResultVerifications
                .FirstOrDefaultAsync(v =>
                    v.TestResultVerificationId == id);

            if (verification == null)
            {
                return NotFound();
            }

            verification.ReviewNotes = reviewNotes;
            verification.VerificationDateTime = DateTime.Now;

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

            return RedirectToAction(nameof(ReviewResults));
        }


    }
}

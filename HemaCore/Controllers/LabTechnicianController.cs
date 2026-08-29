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
                return View(new LabTechnicianDashboardViewModel());
            }


            var myTests = _context.TestRequestTestTypes
                .Include(t => t.TestRequest)
                    .ThenInclude(r => r.Patient)
                .Include(t => t.TestType)
                .Where(t =>
                    t.LabTechnicianId == technician.LabTechnicianId);


            

            var dashboard = new LabTechnicianDashboardViewModel
            {

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
        // RECEIVE SAMPLE
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
    }
}
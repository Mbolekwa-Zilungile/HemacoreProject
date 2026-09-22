using HemaCore.Data;
using HemaCore.Models;
using HemaCore.ViewModels;
using HemaCore.ViewModels.Patient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemaCore.Controllers
{
    public class PatientController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Users> _userManager;

        public PatientController(AppDbContext context, UserManager<Users> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ---------------------------------------------------------------------
        // Helper: get the Patient record for the currently logged-in user
        // ---------------------------------------------------------------------
        private async Task<Patient?> GetCurrentPatientAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            return await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == user.Id);
        }

        // ---------------------------------------------------------------------
        // Dashboard
        // ---------------------------------------------------------------------
        public async Task<IActionResult> Dashboard()
        {
            var patient = await GetCurrentPatientAsync();
            return View(patient);
        }

        // ---------------------------------------------------------------------
        // Complete Profile (GET)
        // ---------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> CompleteProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // If patient record already exists, prefill
            var existing = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            var vm = new CompleteProfileViewModel
            {
                AvailableConditions = await _context.Conditions
                    .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                AvailableAllergies = await _context.Allergies
                    .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(),
                AvailableMedications = await _context.Medications
                    .Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync()
            };

            if (existing != null)
            {
                vm.Name = existing.Name;
                vm.Surname = existing.Surname;
                vm.IDNumber = existing.IDNumber;
                vm.Gender = existing.Gender;
                vm.PhoneNumber = existing.PhoneNumber;
                vm.DateOfBirth = existing.DateOfBirth;
                vm.Address = existing.Address;

                vm.SelectedConditionIds = await _context.PatientConditions
                    .Where(pc => pc.PatientID == existing.PatientID)
                    .Select(pc => pc.ConditionId).ToListAsync();

                vm.SelectedAllergyIds = await _context.PatientAllergies
                    .Where(pa => pa.PatientID == existing.PatientID)
                    .Select(pa => pa.AllergyId).ToListAsync();

                vm.SelectedMedicationIds = await _context.PatientMedications
                    .Where(pm => pm.PatientID == existing.PatientID)
                    .Select(pm => pm.MedicationId).ToListAsync();
            }

            return View(vm);
        }

        // ---------------------------------------------------------------------
        // Complete Profile (POST)
        // ---------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteProfile(CompleteProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                model.AvailableConditions = await _context.Conditions
                    .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
                model.AvailableAllergies = await _context.Allergies
                    .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();
                model.AvailableMedications = await _context.Medications
                    .Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync();
                return View(model);
            }

            // 1. Create or update the Patient
            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null)
            {
                patient = new Patient { UserId = user.Id };
                _context.Patients.Add(patient);
            }

            patient.Name = model.Name;
            patient.Surname = model.Surname;
            patient.IDNumber = model.IDNumber;
            patient.Gender = model.Gender;
            patient.PhoneNumber = model.PhoneNumber;
            patient.DateOfBirth = model.DateOfBirth;
            patient.Address = model.Address ?? "";

            await _context.SaveChangesAsync();  // ensures PatientID is set

            // 2. Sync conditions
            var existingConditions = _context.PatientConditions
                .Where(pc => pc.PatientID == patient.PatientID);
            _context.PatientConditions.RemoveRange(existingConditions);

            foreach (var cid in model.SelectedConditionIds.Distinct())
            {
                _context.PatientConditions.Add(new PatientCondition
                {
                    PatientID = patient.PatientID,
                    ConditionId = cid
                });
            }

            // 3. Sync allergies
            var existingAllergies = _context.PatientAllergies
                .Where(pa => pa.PatientID == patient.PatientID);
            _context.PatientAllergies.RemoveRange(existingAllergies);

            foreach (var aid in model.SelectedAllergyIds.Distinct())
            {
                _context.PatientAllergies.Add(new PatientAllergy
                {
                    PatientID = patient.PatientID,
                    AllergyId = aid
                });
            }

            // 4. Sync medications
            var existingMeds = _context.PatientMedications
                .Where(pm => pm.PatientID == patient.PatientID);
            _context.PatientMedications.RemoveRange(existingMeds);

            foreach (var mid in model.SelectedMedicationIds.Distinct())
            {
                _context.PatientMedications.Add(new PatientMedication
                {
                    PatientID = patient.PatientID,
                    MedicationId = mid
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Profile saved successfully!";
            return RedirectToAction(nameof(Dashboard));
        }

        // ---------------------------------------------------------------------
        // Patient Details
        // ---------------------------------------------------------------------
        public async Task<IActionResult> PatientDetails()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction(nameof(CompleteProfile));

            // Eager-load the reference data so the view can display names
            var conditions = await _context.PatientConditions
                .Where(pc => pc.PatientID == patient.PatientID)
                .Include(pc => pc.Condition)
                .ToListAsync();

            var allergies = await _context.PatientAllergies
                .Where(pa => pa.PatientID == patient.PatientID)
                .Include(pa => pa.Allergy)
                .ToListAsync();

            var medications = await _context.PatientMedications
                .Where(pm => pm.PatientID == patient.PatientID)
                .Include(pm => pm.Medication)
                .ToListAsync();

            ViewBag.Conditions = conditions;
            ViewBag.Allergies = allergies;
            ViewBag.Medications = medications;

            return View(patient);
        }

        // ---------------------------------------------------------------------
        // Edit Profile (GET)
        // ---------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null) return RedirectToAction(nameof(CompleteProfile));

            var vm = new EditPatientProfileViewModel
            {
                Email = user.Email ?? "",
                Name = patient.Name,
                Surname = patient.Surname,
                IDNumber = patient.IDNumber,
                PhoneNumber = patient.PhoneNumber,
                Gender = patient.Gender,
                DateOfBirth = patient.DateOfBirth,
                Address = patient.Address,

                SelectedConditionIds = await _context.PatientConditions
                    .Where(pc => pc.PatientID == patient.PatientID)
                    .Select(pc => pc.ConditionId).ToListAsync(),

                SelectedAllergyIds = await _context.PatientAllergies
                    .Where(pa => pa.PatientID == patient.PatientID)
                    .Select(pa => pa.AllergyId).ToListAsync(),

                SelectedMedicationIds = await _context.PatientMedications
                    .Where(pm => pm.PatientID == patient.PatientID)
                    .Select(pm => pm.MedicationId).ToListAsync(),

                AvailableConditions = await _context.Conditions
                    .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                AvailableAllergies = await _context.Allergies
                    .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync(),
                AvailableMedications = await _context.Medications
                    .Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync()
            };

            return View(vm);
        }

        // ---------------------------------------------------------------------
        // Edit Profile (POST)
        // ---------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditPatientProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                model.AvailableConditions = await _context.Conditions
                    .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
                model.AvailableAllergies = await _context.Allergies
                    .Where(a => a.IsActive).OrderBy(a => a.Name).ToListAsync();
                model.AvailableMedications = await _context.Medications
                    .Where(m => m.IsActive).OrderBy(m => m.Name).ToListAsync();
                return View(model);
            }

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (patient == null) return NotFound();

            // Update patient
            patient.Name = model.Name;
            patient.Surname = model.Surname;
            patient.IDNumber = model.IDNumber;
            patient.PhoneNumber = model.PhoneNumber;
            patient.Gender = model.Gender;
            patient.DateOfBirth = model.DateOfBirth;
            patient.Address = model.Address ?? "";

            // Update Identity email if changed
            if (user.Email != model.Email)
            {
                user.Email = model.Email;
                user.UserName = model.Email;
                await _userManager.UpdateAsync(user);
            }

            // Sync selected reference data
            _context.PatientConditions.RemoveRange(
                _context.PatientConditions.Where(pc => pc.PatientID == patient.PatientID));
            foreach (var cid in model.SelectedConditionIds.Distinct())
                _context.PatientConditions.Add(new PatientCondition
                {
                    PatientID = patient.PatientID,
                    ConditionId = cid
                });

            _context.PatientAllergies.RemoveRange(
                _context.PatientAllergies.Where(pa => pa.PatientID == patient.PatientID));
            foreach (var aid in model.SelectedAllergyIds.Distinct())
                _context.PatientAllergies.Add(new PatientAllergy
                {
                    PatientID = patient.PatientID,
                    AllergyId = aid
                });

            _context.PatientMedications.RemoveRange(
                _context.PatientMedications.Where(pm => pm.PatientID == patient.PatientID));
            foreach (var mid in model.SelectedMedicationIds.Distinct())
                _context.PatientMedications.Add(new PatientMedication
                {
                    PatientID = patient.PatientID,
                    MedicationId = mid
                });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(EditProfile));
        }

        // ---------------------------------------------------------------------
        // Change Password
        // ---------------------------------------------------------------------
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var result = await _userManager.ChangePasswordAsync(
                user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError("", err.Description);
                return View(model);
            }

            await _userManager.UpdateSecurityStampAsync(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully. Please log in again.";
            return RedirectToAction("Login", "Account");
        }

        // ---------------------------------------------------------------------
        // Stub pages
        // ---------------------------------------------------------------------
        public IActionResult Tests() => View();
        public IActionResult Result() => View();
        public IActionResult Health() => View();
        public IActionResult Consent() => View();
        public IActionResult Report() => View();
        public IActionResult Index() => View();
    }
}
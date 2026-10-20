using HemaCore.Data;
using HemaCore.Models;
using HemaCore.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Eventing.Reader;

namespace HemaCore.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private AppDbContext _context;

        public AccountController(SignInManager<Users> signInManager, UserManager<Users> userManager, AppDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.PhoneNumber = model.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);

                TempData["Success"] = "Profile updated successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);
                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Doctor"))
                    {
                        return RedirectToAction("Index", "Doctor");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "LabTechnician"))
                    {
                        return RedirectToAction("Index", "LabTechnician");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Manager"))
                    {
                        return RedirectToAction("Index", "Manager");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Patient"))
                    {
                        return RedirectToAction("Index", "Patient");
                    }
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError("", "Invalid login");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Check whether the email is already in use
            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Email already exists.");
                return View(model);
            }

            var user = new Users
            {
                FullName = $"{model.Name} {model.Surname}",
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View(model);
            }

            // Assign the Patient role
            await _userManager.AddToRoleAsync(user, "Patient");

            // Create the patient record
            var patient = new Patient
            {
                Name = model.Name,
                Surname = model.Surname,
                IDNumber = model.IDNumber,
                DateOfBirth = model.DateOfBirth,
                PhoneNumber = model.PhoneNumber,
                StreetAddress = model.StreetAddress,
                Suburb = model.Suburb,
                City = model.City,
                Province = model.Province,
                PostalCode = model.PostalCode,
                Gender = model.Gender,
                UserId = user.Id
            };

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            // Sign the user in
            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToAction("EditProfile", "Patient");
        }

        // GET: Account/Logout
        // Logout works from a link (GET) or a form button (POST).
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}

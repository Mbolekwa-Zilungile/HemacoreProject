using HemaCore.Data;
using HemaCore.Models;
using HemaCore.Services;
using HemaCore.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemaCore.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly IEmailService _emailService;
        private readonly AppDbContext _context;

        public AccountController(
            SignInManager<Users> signInManager,
            UserManager<Users> userManager,
            AppDbContext context,
            IEmailService emailService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _emailService = emailService;
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
            return View(new LoginViewModel());
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user != null)
                {
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                        return RedirectToAction("Index", "Admin");

                    if (await _userManager.IsInRoleAsync(user, "Doctor"))
                        return RedirectToAction("Index", "Doctor");

                    if (await _userManager.IsInRoleAsync(user, "LabTechnician"))
                        return RedirectToAction("Index", "LabTechnician");

                    if (await _userManager.IsInRoleAsync(user, "Manager"))
                        return RedirectToAction("Index", "Manager");

                    if (await _userManager.IsInRoleAsync(user, "Patient"))
                        return RedirectToAction("Index", "Patient");
                }

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty,
                    "This account has been locked out. Please try again later.");
            }
            else if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty,
                    "This account is not allowed to sign in. Please confirm your email.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }

            return View(model);
        }

        // GET: Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email already exists.");

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

            var result = await _userManager.CreateAsync(
                user,
                model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Patient");

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

            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToAction("EditProfile", "Patient");
        }

        // GET: Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }
        // POST: Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            // Do not reveal whether an account exists.
            if (user == null)
                return RedirectToAction(nameof(ForgotPasswordConfirmation));

            var token =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            var resetLink = Url.Action(
                nameof(ResetPassword),
                "Account",
                new
                {
                    email = model.Email,
                    token = token
                },
                protocol: Request.Scheme);

            if (string.IsNullOrWhiteSpace(resetLink))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "We could not create the reset link. Please try again.");

                return View(model);
            }

            try
            {
                await _emailService.SendPasswordResetEmailAsync(
                    model.Email,
                    resetLink);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "We could not send the reset email right now. Please try again later.");

                return View(model);
            }

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        // GET: Account/ForgotPasswordConfirmation
        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        // GET: Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        // POST: Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
                return RedirectToAction(nameof(ResetPasswordConfirmation));

            var result = await _userManager.ResetPasswordAsync(
                user,
                model.Token,
                model.Password);

            if (result.Succeeded)
                return RedirectToAction(nameof(ResetPasswordConfirmation));

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // GET: Account/ResetPasswordConfirmation
        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        // GET: Account/Logout
        // Logout works from a link (GET) or a form button (POST).
        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}
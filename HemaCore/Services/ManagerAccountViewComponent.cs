using HemaCore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HemaCore.ViewComponents
{
    public class ManagerAccountViewComponent : ViewComponent
    {
        private readonly UserManager<Users> _userManager;

        public ManagerAccountViewComponent(
            UserManager<Users> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(
                ViewContext.HttpContext.User);

            string managerName = "Manager";

            if (user != null &&
                await _userManager.IsInRoleAsync(user, "Manager"))
            {
                managerName = !string.IsNullOrWhiteSpace(user.FullName)
                    ? user.FullName
                    : user.Email ?? "Manager";
            }

            return View("Default", managerName);
        }
    }
}
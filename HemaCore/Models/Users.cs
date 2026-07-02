using Microsoft.AspNetCore.Identity;

namespace HemaCore.Models
{
    public class Users : IdentityUser
    {
        public string FullName { get; set; }
    }
}

using HemaCore.Data;
using HemaCore.Models;
using Microsoft.AspNetCore.Identity;

namespace HemaCore.Services
{
    public class SeedService
    {
        public static async Task SeedDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedService>>();

            try
            {
                //Ensure the database is ready
                logger.LogInformation("Seeding roles.");
                await context.Database.EnsureCreatedAsync();

                // Add roles
                logger.LogInformation("Seeding roles.");
                await AddRoleAsync(roleManager, "Admin");
                await AddRoleAsync(roleManager, "Manager");
                await AddRoleAsync(roleManager, "Doctor");
                await AddRoleAsync(roleManager, "LabTechnician");
                await AddRoleAsync(roleManager, "Patient");

                // add manager user
                logger.LogInformation("Seeding manager user");
                var managerEmail = "manager@gmail.com";
                if(await userManager.FindByEmailAsync(managerEmail) == null)
                {
                    var managerUser = new Users
                    {
                        FullName = "Neo Adams",
                        UserName = managerEmail,
                        NormalizedUserName = managerEmail.ToUpper(),
                        Email = managerEmail,
                        NormalizedEmail = managerEmail.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };
                    var result = await userManager.CreateAsync(managerUser, "Manager@123");
                    if (result.Succeeded)
                    {
                        logger.LogInformation("Assigning Manager role to the manager user.");
                        await userManager.AddToRoleAsync(managerUser, "Manager");
                    }
                    else
                    {
                        logger.LogError("Failed to create manager user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }

                }

                logger.LogInformation("Seeding manager user");
                var adminEmail = "admin@gmail.com";
                if (await userManager.FindByEmailAsync(adminEmail) == null)
                {
                    var adminUser = new Users
                    {
                        FullName = "Esethu Mbolekwa",
                        UserName = adminEmail,
                        NormalizedUserName = adminEmail.ToUpper(),
                        Email = adminEmail,
                        NormalizedEmail = adminEmail.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };
                    var result = await userManager.CreateAsync(adminUser, "Admin@123");
                    if (result.Succeeded)
                    {
                        logger.LogInformation("Assigning Manager role to the manager user.");
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                    }
                    else
                    {
                        logger.LogError("Failed to create manager user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }

                }
                /////LABTECH
                logger.LogInformation("Seeding lab technician user");

                var labTechEmail = "labtech@gmail.com";

                if (await userManager.FindByEmailAsync(labTechEmail) == null)
                {
                    var labTechUser = new Users
                    {
                        FullName = "Didi Lecoge",
                        UserName = labTechEmail,
                        NormalizedUserName = labTechEmail.ToUpper(),
                        Email = labTechEmail,
                        NormalizedEmail = labTechEmail.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };

                    var result = await userManager.CreateAsync(labTechUser, "Welcome@2026");

                    if (result.Succeeded)
                    {
                        logger.LogInformation("Assigning LabTechnician role to lab technician user.");
                        await userManager.AddToRoleAsync(labTechUser, "LabTechnician");
                    }
                    else
                    {
                        logger.LogError(
                            "Failed to create lab technician user: {Errors}",
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }


            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database,");
            }
        }

        public static async Task AddRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if(!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }

    }
}

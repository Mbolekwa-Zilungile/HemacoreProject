using HemaCore.Data;
using HemaCore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
                if (await userManager.FindByEmailAsync(managerEmail) == null)
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

                // =============================================
                // SECOND LAB TECHNICIAN - TEST ACCOUNT
                // =============================================

                logger.LogInformation("Seeding second lab technician user");

                var labTech2Email = "labtech2@gmail.com";

                var labTech2User = await userManager.FindByEmailAsync(labTech2Email);

                if (labTech2User == null)
                {
                    labTech2User = new Users
                    {
                        FullName = "Test Technician",
                        UserName = labTech2Email,
                        NormalizedUserName = labTech2Email.ToUpper(),
                        Email = labTech2Email,
                        NormalizedEmail = labTech2Email.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };

                    var result = await userManager.CreateAsync(
                        labTech2User,
                        "Welcome@2026"
                    );

                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(
                            labTech2User,
                            "LabTechnician"
                        );

                        logger.LogInformation(
                            "Second lab technician user created."
                        );
                    }
                }

                // Create the LabTechnicians table record
                if (labTech2User != null)
                {
                    var technician2 = await context.LabTechnicians
                        .FirstOrDefaultAsync(t => t.UserId == labTech2User.Id);

                    if (technician2 == null)
                    {
                        technician2 = new LabTechnician
                        {
                            Name = "Test",
                            Surname = "Technician",
                            IsActive = true,
                            IDNumber = "9001015800081",
                            EmployeeNumber = "LT002",
                            PhoneNumber = "+27820000002",
                            UserId = labTech2User.Id
                        };

                        context.LabTechnicians.Add(technician2);

                        await context.SaveChangesAsync();

                        logger.LogInformation(
                            "Second lab technician record created."
                        );
                    }

                    // Only assign TestType 4 if it exists, so a missing
                    // test type does not crash the seeder
                    bool testType4Exists = await context.TestTypes
                        .AnyAsync(t => t.TestTypeId == 4);

                    if (testType4Exists)
                    {
                        // Give Technician 2 permission to perform Haemoglobin
                        bool haemoglobinAssignmentExists =
                            await context.TechnicianTestTypes
                                .AnyAsync(tt =>
                                    tt.LabTechnicianId == technician2.LabTechnicianId &&
                                    tt.TestTypeId == 4);

                        if (!haemoglobinAssignmentExists)
                        {
                            context.TechnicianTestTypes.Add(
                                new TechnicianTestType
                                {
                                    LabTechnicianId = technician2.LabTechnicianId,
                                    TestTypeId = 4
                                });

                            await context.SaveChangesAsync();

                            logger.LogInformation(
                                "Haemoglobin assigned to second technician."
                            );
                        }
                    }
                    else
                    {
                        logger.LogWarning("TestType 4 not found, skipping technician assignment.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database,");
            }

            // Doctor is seeded separately so nothing above can block it
            await SeedDoctorAsync(context, roleManager, userManager, logger);
        }

        private static async Task SeedDoctorAsync(
            AppDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<Users> userManager,
            ILogger<SeedService> logger)
        {
            try
            {
                await AddRoleAsync(roleManager, "Doctor");

                var doctorEmail = "LihleDlilanga@gmail.com";
                var doctorUser = await userManager.FindByEmailAsync(doctorEmail);

                if (doctorUser == null)
                {
                    doctorUser = new Users
                    {
                        FullName = "Lihle Dlilanga",
                        UserName = doctorEmail,
                        Email = doctorEmail,
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(doctorUser, "Bombibethu@2026");

                    if (!result.Succeeded)
                    {
                        logger.LogError(
                            "Doctor seeding failed: {Errors}",
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                        return;
                    }
                }

                if (!await userManager.IsInRoleAsync(doctorUser, "Doctor"))
                {
                    await userManager.AddToRoleAsync(doctorUser, "Doctor");
                }

                bool doctorRecordExists = await context.Doctors
                    .AnyAsync(d => d.UserId == doctorUser.Id);

                if (!doctorRecordExists)
                {
                    context.Doctors.Add(new Doctor
                    {
                        Name = "Lihle",
                        Surname = "Dlilanga",
                        HPCSANumber = "MP0123456",
                        PhoneNumber = "+27674578698",
                        UserId = doctorUser.Id,
                        IsActive = true
                    });

                    await context.SaveChangesAsync();
                }

                logger.LogInformation("Doctor seeding finished.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Doctor seeding failed.");
            }
        }

        public static async Task AddRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
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
using HemaCore.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace HemaCore.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Sample> Samples { get; set; }
        public DbSet<TestRequest> TestRequests { get; set; }
        public DbSet<LabTechnician> LabTechnicians { get; set; }
        public DbSet<TestRequestItem> TestRequestTestTypes { get; set; }
        public DbSet<TestResult> TestResults { get; set; }
        public DbSet<TechnicianTestType> TechnicianTestTypes { get; set; }
        public DbSet<TestResultVerification> TestResultVerifications { get; set; }
        public DbSet<TestCategory> TestCategories { get; set; }
        public DbSet<TestType> TestTypes { get; set; }
        public DbSet<TestTypeSample> TestTypeSamples { get; set; }
        public DbSet<SampleType> SampleTypes { get; set; }
        public DbSet<TestTypeConsumable> testTypeConsumables { get; set; }
        public DbSet<Consumable> Consumables { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Patient> Patients { get; set; }

        public DbSet<Doctor> Doctors => Set<Doctor>();
        public DbSet<ConditionCategory> ConditionCategories => Set<ConditionCategory>();
        public DbSet<Condition> Conditions => Set<Condition>();
        public DbSet<AllergyCategory> AllergyCategories => Set<AllergyCategory>();
        public DbSet<Allergy> Allergies => Set<Allergy>();
        public DbSet<MedicationCategory> MedicationCategories => Set<MedicationCategory>();
        public DbSet<Medication> Medications => Set<Medication>();
        public DbSet<PatientCondition> PatientConditions => Set<PatientCondition>();
        public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();
        public DbSet<PatientMedication> PatientMedications => Set<PatientMedication>();
        public DbSet<Consent> Consents => Set<Consent>();
        public DbSet<ConsentTestRequest> ConsentTestAccesses => Set<ConsentTestRequest>();

        protected override void OnModelCreating(ModelBuilder modelbuilder)
        {
            base.OnModelCreating(modelbuilder);

            //One-to-one: TestResult - TestResultVerification
            modelbuilder.Entity<TestResult>()
                .HasOne(r => r.TestResultVerification)
                .WithOne(v => v.TestResult)
                .HasForeignKey<TestResultVerification>(v => v.TestResultId);

            //Composite Primary key for technicianTestType
            modelbuilder.Entity<TechnicianTestType>()
                .HasKey(t => new
                { t.LabTechnicianId, t.TestTypeId });
            //Composite Primary key for TestRequestTestType
            modelbuilder.Entity<TestRequestItem>()
    .HasKey(t => t.TestRequestTestTypeId);
            //Composite Primary key for TestTypeSample
            modelbuilder.Entity<TestTypeSample>()
                .HasKey(t => new
                { t.TestTypeId, t.SampleTypeId });
            //Composite Primary key for TestTypeConsumable
            modelbuilder.Entity<TestTypeConsumable>()
                .HasKey(t => new
                { t.TestTypeId, t.ConsumableId });

            modelbuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.NoAction);

            modelbuilder.Entity<PatientMedication>()
                .HasKey(pm => pm.Id);

            modelbuilder.Entity<PatientMedication>()
                .HasOne(pm => pm.Patient)
                .WithMany()
                .HasForeignKey(pm => pm.PatientId)
                .OnDelete(DeleteBehavior.NoAction);

            modelbuilder.Entity<PatientMedication>()
                .HasOne(pm => pm.Medication)
                .WithMany(m => m.PatientMedications)
                .HasForeignKey(pm => pm.MedicationId)
                .OnDelete(DeleteBehavior.NoAction);


            modelbuilder.Entity<Supplier>()
                .Ignore(s => s.Suppliers);
            // Alert decimal precision
            modelbuilder.Entity<Alert>()
                .Property(a => a.NormalRangeMax)
                .HasPrecision(18, 2);

            modelbuilder.Entity<Alert>()
                .Property(a => a.NormalRangeMin)
                .HasPrecision(18, 2);

            modelbuilder.Entity<Alert>()
                .Property(a => a.ResultValue)
                .HasPrecision(18, 2);
            modelbuilder.Entity<ConsentTestRequest>()
    .HasOne(x => x.TestRequest)
    .WithMany()
    .HasForeignKey(x => x.TestRequestId)
    .OnDelete(DeleteBehavior.NoAction);

            modelbuilder.Entity<ConsentTestRequest>()
                .HasOne(x => x.Consent)
                .WithMany()
                .HasForeignKey(x => x.ConsentId)
                .OnDelete(DeleteBehavior.Cascade);



            modelbuilder.Entity<Doctor>()
     .HasOne(d => d.User)
     .WithMany()
     .HasForeignKey(d => d.UserId)
     .OnDelete(DeleteBehavior.NoAction);


        }


    }

}

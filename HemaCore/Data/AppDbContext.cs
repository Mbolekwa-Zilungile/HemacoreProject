using HemaCore.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HemaCore.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // ==========================================
        // DbSets
        // ==========================================

        public DbSet<Alert> Alerts => Set<Alert>();

        public DbSet<Sample> Samples { get; set; }

        public DbSet<TestRequest> TestRequests { get; set; }

        public DbSet<LabTechnician> LabTechnicians { get; set; }

        public DbSet<TestRequestItem> TestRequestTestTypes { get; set; }

        public DbSet<TestResult> TestResults { get; set; }

        public DbSet<TechnicianTestType> TechnicianTestTypes { get; set; }

        public DbSet<TestResultVerification> TestResultVerifications { get; set; }

        public DbSet<TestCategory> TestCategories { get; set; }

        public DbSet<TestType> TestTypes { get; set; }

        public DbSet<SampleType> SampleTypes { get; set; }

        public DbSet<TestTypeConsumable> testTypeConsumables { get; set; }

        public DbSet<Consumable> Consumables { get; set; }

        public DbSet<Supplier> Suppliers { get; set; }

        public DbSet<OrderItem> OrderItems { get; set; }

        public DbSet<Order> Orders { get; set; }

        public DbSet<Patient> Patients { get; set; }

        public DbSet<Doctor> Doctors => Set<Doctor>();

        public DbSet<ConditionCategory> ConditionCategories
            => Set<ConditionCategory>();

        public DbSet<Condition> Conditions
            => Set<Condition>();

        public DbSet<AllergyCategory> AllergyCategories
            => Set<AllergyCategory>();

        public DbSet<Allergy> Allergies
            => Set<Allergy>();

        public DbSet<MedicationCategory> MedicationCategories
            => Set<MedicationCategory>();

        public DbSet<Medication> Medications
            => Set<Medication>();

        public DbSet<PatientCondition> PatientConditions
            => Set<PatientCondition>();

        public DbSet<PatientAllergy> PatientAllergies
            => Set<PatientAllergy>();

        public DbSet<PatientMedication> PatientMedications
            => Set<PatientMedication>();

        public DbSet<Consent> Consents
            => Set<Consent>();

        public DbSet<ConsentTestRequest> ConsentTestAccesses
            => Set<ConsentTestRequest>();

        public DbSet<ConsentCondition> ConsentConditions
        { get; set; }


        protected override void OnModelCreating(ModelBuilder modelbuilder)
        {
            base.OnModelCreating(modelbuilder);


            // =====================================================
            // TestResult - TestResultVerification
            // One-to-One Relationship
            // =====================================================

            modelbuilder.Entity<TestResult>()
                .HasOne(r => r.TestResultVerification)
                .WithOne(v => v.TestResult)
                .HasForeignKey<TestResultVerification>(
                    v => v.TestResultId
                );


            // =====================================================
            // TechnicianTestType
            // Composite Primary Key
            // =====================================================

            modelbuilder.Entity<TechnicianTestType>()
                .HasKey(t => new
                {
                    t.LabTechnicianId,
                    t.TestTypeId
                });


            // =====================================================
            // TestRequestItem
            // Primary Key
            // =====================================================

            modelbuilder.Entity<TestRequestItem>()
                .HasKey(t => t.TestRequestTestTypeId);


            // =====================================================
            // TestType - SampleType
            //
            // One SampleType
            //     ↓
            // Many TestTypes
            //
            // SampleType
            //      1
            //      |
            //      |
            //      *
            //   TestType
            // =====================================================

            modelbuilder.Entity<TestType>()
                .HasOne(t => t.SampleType)
                .WithMany(s => s.TestTypes)
                .HasForeignKey(t => t.SampleTypeId)
                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // TestType - TestCategory
            //
            // One TestCategory
            //     ↓
            // Many TestTypes
            // =====================================================

            modelbuilder.Entity<TestType>()
                .HasOne(t => t.Category)
                .WithMany(c => c.TestTypes)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // TestTypeConsumable
            // Composite Primary Key
            // =====================================================

            modelbuilder.Entity<TestTypeConsumable>()
                .HasKey(t => new
                {
                    t.TestTypeId,
                    t.ConsumableId
                });


            // =====================================================
            // OrderItem - Order
            // =====================================================

            modelbuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // PatientMedication
            // =====================================================

            modelbuilder.Entity<PatientMedication>()
                .HasKey(pm => pm.Id);


            modelbuilder.Entity<PatientMedication>()
                .HasOne(pm => pm.Patient)
                .WithMany()
                .HasForeignKey(pm => pm.PatientID)
                .OnDelete(DeleteBehavior.NoAction);


            modelbuilder.Entity<PatientMedication>()
                .HasOne(pm => pm.Medication)
                .WithMany(m => m.PatientMedications)
                .HasForeignKey(pm => pm.MedicationId)
                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // Supplier
            // =====================================================

            modelbuilder.Entity<Supplier>()
                .Ignore(s => s.Suppliers);


            // =====================================================
            // Alert Decimal Precision
            // =====================================================

            modelbuilder.Entity<Alert>()
                .Property(a => a.NormalRangeMax)
                .HasPrecision(18, 2);

            modelbuilder.Entity<Alert>()
                .Property(a => a.NormalRangeMin)
                .HasPrecision(18, 2);

            modelbuilder.Entity<Alert>()
                .Property(a => a.ResultValue)
                .HasPrecision(18, 2);


            // =====================================================
            // ConsentTestRequest
            // =====================================================

            modelbuilder.Entity<ConsentTestRequest>()
                .HasOne(x => x.TestRequest)
                .WithMany()
                .HasForeignKey(x => x.TestRequestId)
                .OnDelete(DeleteBehavior.NoAction);


            modelbuilder.Entity<ConsentTestRequest>()
                .HasOne(x => x.Consent)
                .WithMany(c => c.TestAccesses)
                .HasForeignKey(x => x.ConsentId)
                .OnDelete(DeleteBehavior.Cascade);


            // =====================================================
            // ConsentCondition
            // =====================================================

            modelbuilder.Entity<ConsentCondition>()
                .HasOne(cc => cc.Consent)
                .WithMany()
                .HasForeignKey(cc => cc.ConsentId)
                .OnDelete(DeleteBehavior.Cascade);


            modelbuilder.Entity<ConsentCondition>()
                .HasOne(cc => cc.Condition)
                .WithMany()
                .HasForeignKey(cc => cc.ConditionId)
                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // Doctor - User
            // =====================================================

            modelbuilder.Entity<Doctor>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // PatientCondition
            // =====================================================

            modelbuilder.Entity<PatientCondition>()
                .HasOne(pc => pc.Patient)
                .WithMany()
                .HasForeignKey(pc => pc.PatientID)
                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // PatientAllergy
            // =====================================================

            modelbuilder.Entity<PatientAllergy>()
                .HasOne(pa => pa.Patient)
                .WithMany()
                .HasForeignKey(pa => pa.PatientID)
                .OnDelete(DeleteBehavior.NoAction);

           
// ==========================================
// SampleType - TestType
// One SampleType has many TestTypes
// One TestType belongs to one SampleType
// ==========================================

modelbuilder.Entity<TestType>()
    .HasOne(t => t.SampleType)
    .WithMany(s => s.TestTypes)
    .HasForeignKey(t => t.SampleTypeId)
    .OnDelete(DeleteBehavior.NoAction);

        }
    }
}


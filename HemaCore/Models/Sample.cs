using System.ComponentModel.DataAnnotations;

namespace HemaCore.Models
{
    public class Sample
    {
        [Key]
        public int SampleId { get; set; }
        public string Barcode { get; set; }
        public DateTime? DateTimeReceived { get; set; }   // now nullable
        public int TestRequestId { get; set; }
        public TestRequest TestRequest { get; set; }
        public int SampleTypeId { get; set; }
        public SampleType SampleType { get; set; }

        public int? LabTechnicianId { get; set; }
        public LabTechnician LabTechnician { get; set; }
    }
}
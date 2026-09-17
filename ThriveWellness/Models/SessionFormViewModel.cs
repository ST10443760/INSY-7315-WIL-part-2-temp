using System.ComponentModel.DataAnnotations;

namespace ThriveWellness.Models
{
    public class SessionFormViewModel
    {
        public int SessionId { get; set; }

        [Required]
        [Display(Name = "Location")]
        public int LocationId { get; set; }

        [Required]
        [Display(Name = "Session Type")]
        public string SessionType { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required]
        [DataType(DataType.Time)]
        public TimeSpan Time { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than 0.")]
        public int Capacity { get; set; }

        public bool IsOpen { get; set; } = true;

        public IEnumerable<Location> Locations { get; set; } = new List<Location>();
    }
}

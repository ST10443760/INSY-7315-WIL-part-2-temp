using System.ComponentModel.DataAnnotations;

namespace ThriveWellness.Models
{
    // View model (with data annotations) for the admin add/edit location
    // form - LocationId is 0 for a new location, or an existing one being
    // edited.
    public class LocationFormViewModel
    {
        public int LocationId { get; set; }

        [Required]
        [Display(Name = "Name")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Address")]
        [StringLength(200)]
        public string Address { get; set; } = string.Empty;
    }
}

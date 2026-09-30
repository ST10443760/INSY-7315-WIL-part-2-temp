using System.ComponentModel.DataAnnotations;

namespace ThriveWellness.Models
{
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

using System.ComponentModel.DataAnnotations;

namespace ThriveWellness.Models
{
    // View model (with data annotations) for the admin login form - checked
    // against AuthService.Login.
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}

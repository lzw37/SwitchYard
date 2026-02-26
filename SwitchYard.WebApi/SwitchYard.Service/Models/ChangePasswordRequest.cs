using System.ComponentModel.DataAnnotations;

namespace SwitchYard.Service.Models
{
    /// <summary>
    /// Current user password change request.
    /// </summary>
    public class ChangePasswordRequest
    {
        /// <summary>
        /// Current password hash (SHA-256 text from frontend).
        /// </summary>
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        /// <summary>
        /// New password hash (SHA-256 text from frontend).
        /// </summary>
        [Required]
        public string NewPassword { get; set; } = string.Empty;
    }
}

using System.ComponentModel.DataAnnotations;

namespace SwitchYard.Service.Models
{
    /// <summary>
    /// Current user profile update request.
    /// </summary>
    public class UpdateUserInfoRequest
    {
        /// <summary>
        /// Email address. Empty string will clear email.
        /// </summary>
        [MaxLength(320)]
        public string? Email { get; set; }
    }
}

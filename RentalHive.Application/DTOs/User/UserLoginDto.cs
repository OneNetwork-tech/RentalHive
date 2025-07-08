using System.ComponentModel.DataAnnotations;

namespace RentalHive.Application.DTOs.User
{
    /// <summary>
    /// DTO for user login.
    /// </summary>
    public class UserLoginDto
    {
        [Required]
        public string PersonalIdentityNumber { get; set; }

        [Required]
        public string Password { get; set; }
    }
}

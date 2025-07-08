using System.ComponentModel.DataAnnotations;

namespace RentalHive.Application.DTOs.User
{
    /// <summary>
    /// DTO for user registration.
    /// </summary>
    public class UserRegisterDto
    {
        [Required]
        public string PersonalIdentityNumber { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MinLength(8)]
        public string Password { get; set; }
    }
}

using RentalHive.Application.Validation; // Add this using directive
using System.ComponentModel.DataAnnotations;

namespace RentalHive.Application.DTOs.User
{
    public class UserRegisterDto
    {
        [Required(ErrorMessage = "Personnummer is required.")]
        [Personnummer] // Apply our custom validation attribute
        public string PersonalIdentityNumber { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        public string Password { get; set; }
    }
}

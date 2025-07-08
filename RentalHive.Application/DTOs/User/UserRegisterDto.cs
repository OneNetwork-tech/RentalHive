using System.ComponentModel.DataAnnotations;

namespace RentalHive.Application.DTOs.User
{
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
        public string PhoneNumber { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        [MinLength(8)]
        public string Password { get; set; }
    }
}

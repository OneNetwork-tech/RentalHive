using System.ComponentModel.DataAnnotations;

namespace RentalHive.Application.DTOs.User
{
    public class UserLoginDto
    {
        [Required]
        public string LoginIdentifier { get; set; } // Can be Personnummer, Email, or Phone

        [Required]
        public string Password { get; set; }
    }
}

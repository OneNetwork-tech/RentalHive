namespace RentalHive.Application.DTOs.User
{
    /// <summary>
    /// DTO for displaying user details securely.
    /// </summary>
    public class UserDetailsDto
    {
        public int Id { get; set; }
        public string PersonalIdentityNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public DateTime MemberSince { get; set; }
    }
}

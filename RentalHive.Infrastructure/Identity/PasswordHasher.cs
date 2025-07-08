using RentalHive.Application.Contracts.Identity;

namespace RentalHive.Infrastructure.Identity
{
    
    public class PasswordHasher : IPasswordHasher
    {
        
        /// <param name="password">The plain-text password to hash.</param>
        /// <returns>The hashed password string.</returns>
        public string HashPassword(string password)
        {
            // The BCrypt.HashPassword method handles salt generation automatically.
            // We use the fully qualified name to avoid ambiguity.
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        /// <summary>
        /// Verifies that a provided plain-text password matches a stored hash.
        /// </summary>
        /// <param name="hashedPassword">The stored password hash from the database.</param>
        /// <param name="providedPassword">The plain-text password provided by the user.</param>
        /// <returns>True if the password is valid, otherwise false.</returns>
        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            // We use the fully qualified name to avoid ambiguity.
            return BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
        }
    }
}

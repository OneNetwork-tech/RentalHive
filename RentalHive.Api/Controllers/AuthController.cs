using Microsoft.AspNetCore.Mvc;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Application.DTOs.User;
using RentalHive.Domain.Entities;

namespace RentalHive.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;

        // Note: In a real application, you would also inject a password hashing service
        // and a JWT token generation service.

        public AuthController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register(UserRegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var isUnique = await _userRepository.IsPersonalIdentityNumberUnique(registerDto.PersonalIdentityNumber);
            if (!isUnique)
            {
                return BadRequest("Personal Identity Number already exists.");
            }

            // In a real app, hash the password before saving.
            // var hashedPassword = _passwordHasher.Hash(registerDto.Password);
            var hashedPassword = registerDto.Password; // Placeholder

            var user = new User
            {
                PersonalIdentityNumber = registerDto.PersonalIdentityNumber,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                Email = registerDto.Email,
                HashedPassword = hashedPassword,
                MemberSince = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);

            return StatusCode(201);
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login(UserLoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userRepository.GetUserByPersonalIdentityNumberAsync(loginDto.PersonalIdentityNumber);

            if (user == null)
            {
                return Unauthorized("Invalid credentials.");
            }

            // In a real app, verify the hashed password.
            // var isPasswordValid = _passwordHasher.Verify(user.HashedPassword, loginDto.Password);
            var isPasswordValid = user.HashedPassword == loginDto.Password; // Placeholder

            if (!isPasswordValid)
            {
                return Unauthorized("Invalid credentials.");
            }

            // If credentials are valid, generate and return a JWT token.
            // var token = _tokenService.GenerateToken(user);
            var token = "dummy.jwt.token"; // Placeholder

            return Ok(new { Token = token });
        }
    }
}

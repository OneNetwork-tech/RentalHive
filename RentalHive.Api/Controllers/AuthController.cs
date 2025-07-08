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

        public AuthController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register(UserRegisterDto registerDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (!await _userRepository.IsPersonalIdentityNumberUnique(registerDto.PersonalIdentityNumber))
                return BadRequest("Personal Identity Number already exists.");

            if (!await _userRepository.IsEmailUnique(registerDto.Email))
                return BadRequest("Email address already exists.");

            var user = new User
            {
                PersonalIdentityNumber = registerDto.PersonalIdentityNumber,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                Email = registerDto.Email,
                PhoneNumber = registerDto.PhoneNumber,
                Address = registerDto.Address,
                HashedPassword = registerDto.Password, // Placeholder for hashing
                MemberSince = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);
            return StatusCode(201);
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login(UserLoginDto loginDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userRepository.GetUserByIdentifierAsync(loginDto.LoginIdentifier);

            if (user == null || user.HashedPassword != loginDto.Password) // Placeholder for hash verification
            {
                return Unauthorized("Invalid credentials.");
            }

            var token = "dummy.jwt.token"; // Placeholder for real token generation
            return Ok(new { Token = token });
        }
    }
}

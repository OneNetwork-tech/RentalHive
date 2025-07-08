using Microsoft.AspNetCore.Mvc;
using RentalHive.Application.Contracts.Identity;
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
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;

        public AuthController(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator jwtTokenGenerator)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _jwtTokenGenerator = jwtTokenGenerator;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterDto registerDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

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
                HashedPassword = _passwordHasher.HashPassword(registerDto.Password),
                MemberSince = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);
            return StatusCode(201);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(UserLoginDto loginDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userRepository.GetUserByIdentifierAsync(loginDto.LoginIdentifier);

            if (user == null || !_passwordHasher.VerifyPassword(user.HashedPassword, loginDto.Password))
            {
                return Unauthorized("Invalid credentials.");
            }

            var token = _jwtTokenGenerator.GenerateToken(user);
            return Ok(new { Token = token });
        }
    }
}

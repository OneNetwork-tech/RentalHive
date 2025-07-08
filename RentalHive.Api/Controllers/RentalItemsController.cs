using Microsoft.AspNetCore.Mvc;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Application.DTOs.RentalItem;
using RentalHive.Domain.Entities;

namespace RentalHive.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RentalItemsController : ControllerBase
    {
        private readonly IRentalItemRepository _rentalItemRepository;
        private readonly IUserRepository _userRepository;

        public RentalItemsController(IRentalItemRepository rentalItemRepository, IUserRepository userRepository)
        {
            _rentalItemRepository = rentalItemRepository;
            _userRepository = userRepository;
        }

        [HttpPost]
        public async Task<IActionResult> CreateRentalItem([FromBody] RentalItemCreateDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var owner = await _userRepository.GetByIdAsync(createDto.OwnerId);
            if (owner == null)
            {
                return BadRequest("Invalid Owner ID.");
            }

            RentalItem newItem;

            switch (createDto.Category.ToLower())
            {
                case "car":
                    newItem = new Car
                    {
                        Make = createDto.Make,
                        Model = createDto.Model,
                        Year = createDto.Year ?? 0,
                        NumberOfSeats = createDto.NumberOfSeats ?? 0,
                        TransmissionType = createDto.TransmissionType,
                        FuelType = createDto.FuelType
                    };
                    break;
                // Add cases for "boat", "machine", etc. here
                default:
                    return BadRequest("Invalid category specified.");
            }

            // Populate common properties
            newItem.Name = createDto.Name;
            newItem.Description = createDto.Description;
            newItem.DailyRate = createDto.DailyRate;
            newItem.OwnerId = createDto.OwnerId;

            await _rentalItemRepository.AddAsync(newItem);

            return StatusCode(201, newItem);
        }
    }
}

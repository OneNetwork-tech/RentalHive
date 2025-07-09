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

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<RentalItem>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllRentalItems()
        {
            var allItems = await _rentalItemRepository.GetAllAsync();
            return Ok(allItems);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(RentalItem), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRentalItemById(int id)
        {
            var item = await _rentalItemRepository.GetByIdAsync(id);

            if (item == null)
            {
                return NotFound();
            }

            return Ok(item);
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

                case "boat":
                    newItem = new Boat
                    {
                        BoatType = createDto.BoatType,
                        LengthInFeet = createDto.LengthInFeet ?? 0,
                        Capacity = createDto.Capacity ?? 0
                    };
                    break;

                case "machine":
                    newItem = new Machine
                    {
                        MachineType = createDto.MachineType,
                        Horsepower = createDto.Horsepower ?? 0,
                        WeightInKg = createDto.WeightInKg ?? 0
                    };
                    break;

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

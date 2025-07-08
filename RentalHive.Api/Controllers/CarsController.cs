using Microsoft.AspNetCore.Mvc;
using RentalHive.Application.Contracts.Persistence;
using RentalHive.Application.DTOs.RentalItem;
using RentalHive.Domain.Entities;
using RentalHive.Infrastructure.Persistence.Repositories;

namespace RentalHive.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CarsController : ControllerBase
    {
        private readonly IRentalItemRepository _rentalItemRepository;
        private readonly IUserRepository _userRepository;

        public CarsController(IRentalItemRepository rentalItemRepository, IUserRepository userRepository)
        {
            _rentalItemRepository = rentalItemRepository;
            _userRepository = userRepository;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CarDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCars()
        {
            var allItems = await _rentalItemRepository.GetAllAsync();
            var cars = allItems.OfType<Car>(); // Filter to get only Car entities

            // Manual mapping from Entity to DTO
            var carDtos = cars.Select(car => new CarDto
            {
                Id = car.Id,
                Name = car.Name,
                Description = car.Description,
                DailyRate = car.DailyRate,
                OwnerId = car.OwnerId,
                Make = car.Make,
                Model = car.Model,
                Year = car.Year,
                NumberOfSeats = car.NumberOfSeats,
                TransmissionType = car.TransmissionType,
                FuelType = car.FuelType
            });

            return Ok(carDtos);
        }

        [HttpPost]
        [ProducesResponseType(typeof(Car), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCar([FromBody] CreateCarDto createCarDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verify that the owner exists
            var owner = await _userRepository.GetByIdAsync(createCarDto.OwnerId);
            if (owner == null)
            {
                return BadRequest("Invalid Owner ID.");
            }

            // Manual mapping from DTO to Entity
            var car = new Car
            {
                Name = createCarDto.Name,
                Description = createCarDto.Description,
                DailyRate = createCarDto.DailyRate,
                OwnerId = createCarDto.OwnerId,
                Make = createCarDto.Make,
                Model = createCarDto.Model,
                Year = createCarDto.Year,
                NumberOfSeats = createCarDto.NumberOfSeats,
                TransmissionType = createCarDto.TransmissionType,
                FuelType = createCarDto.FuelType
            };

            var createdCar = await _rentalItemRepository.AddAsync(car);

            return CreatedAtAction(nameof(GetCars), new { id = createdCar.Id }, createdCar);
        }
    }
}

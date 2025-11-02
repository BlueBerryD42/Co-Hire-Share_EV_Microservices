using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Booking.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Security.Claims;

namespace CoOwnershipVehicle.Booking.Api.Controllers
{
    [ApiController]
    [Route("api/recurring-bookings")]
    public class RecurringBookingsController : ControllerBase
    {
        private readonly IRecurringBookingService _recurringBookingService;

        public RecurringBookingsController(IRecurringBookingService recurringBookingService)
        {
            _recurringBookingService = recurringBookingService;
        }

        private Guid GetUserId() {
            //This is a placeholder. In a real application, you would get the user ID from the authenticated user's claims.
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException("User ID not found in claims.");
            }
            return Guid.Parse(userId);
        }

        [HttpPost]
        public async Task<IActionResult> CreateRecurringBooking([FromBody] CreateRecurringBookingDto createDto)
        {
            var userId = GetUserId();
            var recurringBooking = await _recurringBookingService.CreateAsync(createDto, userId);
            return CreatedAtAction(nameof(GetRecurringBooking), new { id = recurringBooking.Id }, recurringBooking);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRecurringBooking(Guid id)
        {
            var userId = GetUserId();
            var recurringBooking = await _recurringBookingService.GetByIdAsync(id, userId);
            if (recurringBooking == null)
            {
                return NotFound();
            }
            return Ok(recurringBooking);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRecurringBooking(Guid id, [FromBody] UpdateRecurringBookingDto updateDto)
        {
            var userId = GetUserId();
            var recurringBooking = await _recurringBookingService.UpdateAsync(id, updateDto, userId);
            if (recurringBooking == null)
            {
                return NotFound();
            }
            return Ok(recurringBooking);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecurringBooking(Guid id)
        {
            var userId = GetUserId();
            await _recurringBookingService.CancelAsync(id, userId);
            return NoContent();
        }
    }
}

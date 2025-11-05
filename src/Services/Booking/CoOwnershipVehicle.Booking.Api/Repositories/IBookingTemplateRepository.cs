using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public interface IBookingTemplateRepository
{
    Task AddAsync(BookingTemplate template);
    Task<BookingTemplate?> GetByIdAsync(Guid templateId);
    Task<IReadOnlyList<BookingTemplate>> GetByUserAsync(Guid userId);
    Task UpdateAsync(BookingTemplate template);
    Task DeleteAsync(BookingTemplate template);
    Task SaveChangesAsync();
}
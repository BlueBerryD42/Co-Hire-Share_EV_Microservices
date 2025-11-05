using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Shared.Contracts.DTOs; // For BookingDto, assuming it's shared

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface IBookingTemplateService
{
    Task<BookingTemplateResponse> CreateBookingTemplateAsync(CreateBookingTemplateRequest request, Guid userId);
    Task<IReadOnlyList<BookingTemplateResponse>> GetUserBookingTemplatesAsync(Guid userId);
    Task<BookingTemplateResponse?> GetBookingTemplateByIdAsync(Guid templateId, Guid userId);
    Task<BookingDto> CreateBookingFromTemplateAsync(Guid templateId, CreateBookingFromTemplateRequest request, Guid userId);
    Task<BookingTemplateResponse?> UpdateBookingTemplateAsync(Guid templateId, UpdateBookingTemplateRequest request, Guid userId);
    Task DeleteBookingTemplateAsync(Guid templateId, Guid userId);
}

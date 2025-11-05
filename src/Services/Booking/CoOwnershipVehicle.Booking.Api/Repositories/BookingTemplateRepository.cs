using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public class BookingTemplateRepository : IBookingTemplateRepository
{
    private readonly BookingDbContext _context;

    public BookingTemplateRepository(BookingDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(BookingTemplate template)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }
        await _context.BookingTemplates.AddAsync(template);
    }

    public async Task<BookingTemplate?> GetByIdAsync(Guid templateId)
    {
        return await _context.BookingTemplates
            .Include(t => t.User)
            .Include(t => t.Vehicle)
            .FirstOrDefaultAsync(t => t.Id == templateId);
    }

    public async Task<IReadOnlyList<BookingTemplate>> GetByUserAsync(Guid userId)
    {
        return await _context.BookingTemplates
            .Include(t => t.User)
            .Include(t => t.Vehicle)
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public Task UpdateAsync(BookingTemplate template)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }
        _context.BookingTemplates.Update(template);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(BookingTemplate template)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }
        _context.BookingTemplates.Remove(template);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
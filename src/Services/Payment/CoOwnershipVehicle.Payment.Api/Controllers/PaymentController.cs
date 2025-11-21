using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Payment.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Payment.Api.Services;
using MassTransit;

namespace CoOwnershipVehicle.Payment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly PaymentDbContext _context;
    private readonly IVnPayService _vnPayService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        PaymentDbContext context,
        IVnPayService vnPayService,
        IPublishEndpoint publishEndpoint,
        ILogger<PaymentController> logger)
    {
        _context = context;
        _vnPayService = vnPayService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Create expense for a group
    /// </summary>
    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();

            // TODO: Verify user has access to the group via Group Service HTTP call
            // For now, skip the check - this should be implemented with IGroupServiceClient
            // var hasAccess = await _groupServiceClient.IsUserInGroupAsync(createDto.GroupId, userId, accessToken);
            // if (!hasAccess) return Forbidden(new { message = "Access denied to this group" });

            var expense = new Domain.Entities.Expense
            {
                Id = Guid.NewGuid(),
                GroupId = createDto.GroupId,
                VehicleId = createDto.VehicleId,
                ExpenseType = (Domain.Entities.ExpenseType)createDto.ExpenseType,
                Amount = createDto.Amount,
                Description = createDto.Description,
                DateIncurred = createDto.DateIncurred,
                CreatedBy = userId,
                Notes = createDto.Notes,
                IsRecurring = createDto.IsRecurring,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            // Publish expense created event
            await _publishEndpoint.Publish(new ExpenseCreatedEvent
            {
                ExpenseId = expense.Id,
                GroupId = expense.GroupId,
                VehicleId = expense.VehicleId,
                ExpenseType = (ExpenseType)expense.ExpenseType,
                Amount = expense.Amount,
                Description = expense.Description,
                DateIncurred = expense.DateIncurred,
                CreatedBy = expense.CreatedBy
            });

            // Automatically create cost splitting invoices
            await CreateCostSplittingInvoicesAsync(expense.Id);

            _logger.LogInformation("Expense {ExpenseId} created for group {GroupId}", expense.Id, expense.GroupId);

            var expenseDto = await GetExpenseByIdAsync(expense.Id);
            if (expenseDto == null)
            {
                // This case is unlikely but good to handle. It means the expense was deleted
                // immediately after creation.
                return NotFound(new { message = "Expense created but could not be retrieved." });
            }

            return Ok(expenseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { message = "An error occurred while creating expense" });
        }
    }

    /// <summary>
    /// Get all payments (admin/staff only)
    /// </summary>
    [HttpGet("payments")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> GetAllPayments([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] PaymentStatus? status = null)
    {
        try
        {
            var query = _context.Payments
                .Include(p => p.Invoice)
                    .ThenInclude(i => i.Expense)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(p => p.CreatedAt >= from.Value);
            if (to.HasValue)
                query = query.Where(p => p.CreatedAt <= to.Value);
            if (status.HasValue)
                query = query.Where(p => p.Status == (Domain.Entities.PaymentStatus)status.Value);

            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PaymentDto
                {
                    Id = p.Id,
                    InvoiceId = p.InvoiceId,
                    PayerId = p.PayerId,
                    PayerName = "N/A", // Payer info is in another service
                    Amount = p.Amount,
                    Method = (PaymentMethod)p.Method,
                    Status = (PaymentStatus)p.Status,
                    TransactionReference = p.TransactionReference,
                    Notes = p.Notes,
                    CreatedAt = p.CreatedAt,
                    PaidAt = p.PaidAt
                })
                .ToListAsync();

            return Ok(payments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all payments");
            return StatusCode(500, new { message = "An error occurred while retrieving payments" });
        }
    }

    /// <summary>
    /// Get expenses for user's groups
    /// </summary>
    [HttpGet("expenses/{expenseId}")]
    public async Task<IActionResult> GetExpenseById(Guid expenseId)
    {
        var expenseDto = await GetExpenseByIdAsync(expenseId);
        if (expenseDto == null)
        {
            return NotFound(new { message = "Expense not found." });
        }
        return Ok(expenseDto);
    }

    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses([FromQuery] Guid? groupId = null)
    {
        try
        {
            var userId = GetCurrentUserId();

            // TODO: The query originally filtered expenses based on the user's group membership.
            // This requires a call to the Group service to get the user's groups.
            // For now, we'll just query expenses directly.
            // A more robust solution would involve an API call to the Group service
            // to get a list of group IDs for the current user, then filtering by those IDs.

            var query = _context.Expenses.AsQueryable();

            if (groupId.HasValue)
            {
                // This part can remain as it filters by a provided ID.
                query = query.Where(e => e.GroupId == groupId.Value);
            }

            var expenses = await query
                .OrderByDescending(e => e.DateIncurred)
                .Select(e => new ExpenseDto
                {
                    Id = e.Id,
                    GroupId = e.GroupId,
                    GroupName = "N/A", // Not available in this context
                    VehicleId = e.VehicleId,
                    VehicleModel = "N/A", // Not available in this context
                    ExpenseType = (ExpenseType)e.ExpenseType,
                    Amount = e.Amount,
                    Description = e.Description,
                    DateIncurred = e.DateIncurred,
                    CreatedBy = e.CreatedBy,
                    CreatedByName = "N/A", // Not available in this context
                    Notes = e.Notes,
                    IsRecurring = e.IsRecurring,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses");
            return StatusCode(500, new { message = "An error occurred while retrieving expenses" });
        }
    }

    /// <summary>
    /// Get invoices for user
    /// </summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] Guid? groupId = null)
    {
        try
        {
            var userId = GetCurrentUserId();

            var query = _context.Invoices
                .Include(i => i.Expense) // Expense is in the same context, so this is okay.
                .Where(i => i.PayerId == userId);

            if (groupId.HasValue)
            {
                query = query.Where(i => i.Expense.GroupId == groupId.Value);
            }

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InvoiceDto
                {
                    Id = i.Id,
                    ExpenseId = i.ExpenseId,
                    PayerId = i.PayerId,
                    PayerName = "N/A", // Not available in this context
                    Amount = i.Amount,
                    InvoiceNumber = i.InvoiceNumber,
                    Status = (InvoiceStatus)i.Status,
                    DueDate = i.DueDate,
                    PaidAt = i.PaidAt,
                    CreatedAt = i.CreatedAt,
                    Expense = new ExpenseDto
                    {
                        Id = i.Expense.Id,
                        GroupName = "N/A", // Not available in this context
                        ExpenseType = (ExpenseType)i.Expense.ExpenseType,
                        Description = i.Expense.Description,
                        Amount = i.Expense.Amount,
                        DateIncurred = i.Expense.DateIncurred
                    }
                })
                .ToListAsync();

            return Ok(invoices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invoices");
            return StatusCode(500, new { message = "An error occurred while retrieving invoices" });
        }
    }

    /// <summary>
    /// Create VNPay payment URL for invoice
    /// </summary>
    [HttpPost("vnpay/create-payment")]
    public async Task<IActionResult> CreateVnPayPayment([FromBody] CreateVnPayPaymentDto createDto)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Get the invoice
            var invoice = await _context.Invoices
                .Include(i => i.Expense)
                .FirstOrDefaultAsync(i => i.Id == createDto.InvoiceId && i.PayerId == userId);

            if (invoice == null)
                return NotFound(new { message = "Invoice not found or access denied" });

            if (invoice.Status != Domain.Entities.InvoiceStatus.Pending)
                return BadRequest(new { message = "Invoice is not in pending status" });

            // Create VNPay payment request
            var paymentRequest = new VnPayPaymentRequest
            {
                OrderId = $"INV_{invoice.InvoiceNumber}_{DateTime.Now:yyyyMMddHHmmss}",
                Amount = invoice.Amount,
                OrderInfo = $"Thanh toán hóa đơn {invoice.InvoiceNumber} - {invoice.Expense.Description}",
                OrderType = "vehicle_expense",
                IpAddress = GetClientIpAddress(),
                BankCode = createDto.BankCode
            };

            var paymentUrl = _vnPayService.CreatePaymentUrl(paymentRequest);

            // Create payment record
            var payment = new Domain.Entities.Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                PayerId = userId,
                Amount = invoice.Amount,
                Method = Domain.Entities.PaymentMethod.EWallet, // VNPay is e-wallet
                Status = Domain.Entities.PaymentStatus.Pending,
                TransactionReference = paymentRequest.OrderId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("VNPay payment created for invoice {InvoiceId}, order {OrderId}", 
                invoice.Id, paymentRequest.OrderId);

            return Ok(new
            {
                PaymentUrl = paymentUrl,
                OrderId = paymentRequest.OrderId,
                Amount = invoice.Amount,
                PaymentId = payment.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VNPay payment");
            return StatusCode(500, new { message = "An error occurred while creating payment" });
        }
    }

    /// <summary>
    /// Handle VNPay payment callback
    /// </summary>
    [HttpGet("vnpay/callback")]
    [AllowAnonymous] // VNPay callback doesn't include JWT
    public async Task<IActionResult> VnPayCallback()
    {
        try
        {
            var response = _vnPayService.ProcessPaymentCallback(Request.Query);

            if (!response.Success)
            {
                _logger.LogWarning("VNPay payment failed for order {OrderId}: {Message}", 
                    response.OrderId, response.Message);
                return BadRequest(new { message = response.Message, response });
            }

            // Find the payment record
            var payment = await _context.Payments
                .Include(p => p.Invoice)
                    .ThenInclude(i => i.Expense)
                .FirstOrDefaultAsync(p => p.TransactionReference == response.OrderId);

            if (payment == null)
            {
                _logger.LogError("Payment not found for VNPay order {OrderId}", response.OrderId);
                return NotFound(new { message = "Payment record not found" });
            }

            // Update payment status
            payment.Status = Domain.Entities.PaymentStatus.Completed;
            payment.TransactionReference = response.TransactionId;
            payment.PaidAt = response.PayDate;
            payment.UpdatedAt = DateTime.UtcNow;

            // Update invoice status
            payment.Invoice.Status = Domain.Entities.InvoiceStatus.Paid;
            payment.Invoice.PaidAt = response.PayDate;

            await _context.SaveChangesAsync();

            // Publish payment settled event
            await _publishEndpoint.Publish(new PaymentSettledEvent
            {
                PaymentId = payment.Id,
                InvoiceId = payment.InvoiceId,
                ExpenseId = payment.Invoice.ExpenseId,
                PayerId = payment.PayerId,
                Amount = payment.Amount,
                Method = PaymentMethod.EWallet,
                TransactionReference = response.TransactionId,
                PaidAt = response.PayDate
            });

            _logger.LogInformation("VNPay payment completed for order {OrderId}, amount {Amount} VND", 
                response.OrderId, response.Amount);

            // Redirect to success page (in production, this would be your frontend URL)
            return Ok(new
            {
                Success = true,
                Message = "Payment completed successfully",
                OrderId = response.OrderId,
                TransactionId = response.TransactionId,
                Amount = response.Amount,
                PaymentDate = response.PayDate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay callback");
            return StatusCode(500, new { message = "An error occurred while processing payment" });
        }
    }

    private async Task CreateCostSplittingInvoicesAsync(Guid expenseId)
    {
        // TODO: This method needs to call Group API to get members instead of using navigation property
        // Temporarily skip invoice creation to avoid cross-boundary reference issue
        _logger.LogWarning("Skipping invoice creation for expense {ExpenseId} - Group API integration needed", expenseId);
        await Task.CompletedTask;

        // ORIGINAL CODE COMMENTED OUT - CAUSES CROSS-BOUNDARY REFERENCE ERROR
        // The code below tries to access e.Group which doesn't belong to Payment bounded context
        // Future implementation should call Group API to get member list
    }

    private async Task<ExpenseDto> GetExpenseByIdAsync(Guid expenseId)
    {
        var expense = await _context.Expenses
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null)
        {
            // This scenario should be unlikely if called right after creation,
            // but it's good practice to handle it.
            return null;
        }

        // TODO: In a real-world scenario, you might need to make API calls
        // to other services (Group, Vehicle, User) to enrich this DTO.
        // For now, we return only the data available in the Payment service.
        return new ExpenseDto
        {
            Id = expense.Id,
            GroupId = expense.GroupId,
            GroupName = "N/A", // Not available in this context
            VehicleId = expense.VehicleId,
            VehicleModel = "N/A", // Not available in this context
            ExpenseType = (ExpenseType)expense.ExpenseType,
            Amount = expense.Amount,
            Description = expense.Description,
            DateIncurred = expense.DateIncurred,
            CreatedBy = expense.CreatedBy,
            CreatedByName = "N/A", // Not available in this context
            Notes = expense.Notes,
            IsRecurring = expense.IsRecurring,
            CreatedAt = expense.CreatedAt
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    private string GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    private IActionResult Forbidden(object value)
    {
        return StatusCode(403, value);
    }
}

public class CreateVnPayPaymentDto
{
    public Guid InvoiceId { get; set; }
    public string? BankCode { get; set; }
}

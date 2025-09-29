using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;
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
    private readonly ApplicationDbContext _context;
    private readonly IVnPayService _vnPayService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        ApplicationDbContext context,
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

            // Verify user has access to the group
            var hasAccess = await _context.GroupMembers
                .AnyAsync(m => m.GroupId == createDto.GroupId && m.UserId == userId);

            if (!hasAccess)
                return Forbidden(new { message = "Access denied to this group" });

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

            return Ok(await GetExpenseByIdAsync(expense.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { message = "An error occurred while creating expense" });
        }
    }

    /// <summary>
    /// Get expenses for user's groups
    /// </summary>
    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses([FromQuery] Guid? groupId = null)
    {
        try
        {
            var userId = GetCurrentUserId();

            var query = _context.Expenses
                .Include(e => e.Group)
                    .ThenInclude(g => g.Members)
                .Include(e => e.Vehicle)
                .Include(e => e.Creator)
                .Where(e => e.Group.Members.Any(m => m.UserId == userId));

            if (groupId.HasValue)
            {
                query = query.Where(e => e.GroupId == groupId.Value);
            }

            var expenses = await query
                .OrderByDescending(e => e.DateIncurred)
                .Select(e => new ExpenseDto
                {
                    Id = e.Id,
                    GroupId = e.GroupId,
                    GroupName = e.Group.Name,
                    VehicleId = e.VehicleId,
                    VehicleModel = e.Vehicle != null ? e.Vehicle.Model : null,
                    ExpenseType = (ExpenseType)e.ExpenseType,
                    Amount = e.Amount,
                    Description = e.Description,
                    DateIncurred = e.DateIncurred,
                    CreatedBy = e.CreatedBy,
                    CreatedByName = $"{e.Creator.FirstName} {e.Creator.LastName}",
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
                .Include(i => i.Expense)
                    .ThenInclude(e => e.Group)
                .Include(i => i.Payer)
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
                    PayerName = $"{i.Payer.FirstName} {i.Payer.LastName}",
                    Amount = i.Amount,
                    InvoiceNumber = i.InvoiceNumber,
                    Status = (InvoiceStatus)i.Status,
                    DueDate = i.DueDate,
                    PaidAt = i.PaidAt,
                    CreatedAt = i.CreatedAt,
                    Expense = new ExpenseDto
                    {
                        Id = i.Expense.Id,
                        GroupName = i.Expense.Group.Name,
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
                    .ThenInclude(e => e.Group)
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
                        .ThenInclude(e => e.Group)
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
        var expense = await _context.Expenses
            .Include(e => e.Group)
                .ThenInclude(g => g.Members)
                    .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null) return;

        var members = expense.Group.Members.ToList();
        var invoiceNumber = 1;

        foreach (var member in members)
        {
            var memberAmount = expense.Amount * member.SharePercentage;

            var invoice = new Domain.Entities.Invoice
            {
                Id = Guid.NewGuid(),
                ExpenseId = expense.Id,
                PayerId = member.UserId,
                Amount = memberAmount,
                InvoiceNumber = $"INV-{expense.Id.ToString("N")[..8]}-{invoiceNumber:D3}",
                Status = Domain.Entities.InvoiceStatus.Pending,
                DueDate = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Invoices.Add(invoice);
            invoiceNumber++;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<ExpenseDto> GetExpenseByIdAsync(Guid expenseId)
    {
        return await _context.Expenses
            .Include(e => e.Group)
            .Include(e => e.Vehicle)
            .Include(e => e.Creator)
            .Where(e => e.Id == expenseId)
            .Select(e => new ExpenseDto
            {
                Id = e.Id,
                GroupId = e.GroupId,
                GroupName = e.Group.Name,
                VehicleId = e.VehicleId,
                VehicleModel = e.Vehicle != null ? e.Vehicle.Model : null,
                ExpenseType = (ExpenseType)e.ExpenseType,
                Amount = e.Amount,
                Description = e.Description,
                DateIncurred = e.DateIncurred,
                CreatedBy = e.CreatedBy,
                CreatedByName = $"{e.Creator.FirstName} {e.Creator.LastName}",
                Notes = e.Notes,
                IsRecurring = e.IsRecurring,
                CreatedAt = e.CreatedAt
            })
            .FirstAsync();
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

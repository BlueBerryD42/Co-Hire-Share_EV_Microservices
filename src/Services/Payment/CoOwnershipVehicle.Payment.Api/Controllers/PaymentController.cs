using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Payment.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Payment.Api.Services;
using CoOwnershipVehicle.Payment.Api.Services.Interfaces;
using MassTransit;
using Microsoft.Extensions.Configuration;

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
    private readonly IGroupServiceClient _groupServiceClient;
    private readonly IUserServiceClient _userServiceClient;
    private readonly IFundServiceClient _fundServiceClient;
    private readonly IConfiguration _configuration;

    public PaymentController(
        PaymentDbContext context,
        IVnPayService vnPayService,
        IPublishEndpoint publishEndpoint,
        ILogger<PaymentController> logger,
        IGroupServiceClient groupServiceClient,
        IUserServiceClient userServiceClient,
        IFundServiceClient fundServiceClient,
        IConfiguration configuration)
    {
        _context = context;
        _vnPayService = vnPayService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _groupServiceClient = groupServiceClient;
        _userServiceClient = userServiceClient;
        _fundServiceClient = fundServiceClient;
        _configuration = configuration;
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
            var accessToken = GetAccessToken();

            // Verify user has access to the group via Group Service HTTP call
            // This is required since Payment service doesn't store Group/Member entities
            var hasAccess = await _groupServiceClient.IsUserInGroupAsync(createDto.GroupId, userId, accessToken);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted to create expense for group {GroupId} without access", userId, createDto.GroupId);
                return Forbidden(new { message = "Access denied to this group" });
            }

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
    /// Pay an expense from group fund
    /// Deducts the expense amount from the group's shared fund
    /// </summary>
    [HttpPost("expenses/{expenseId:guid}/pay-from-fund")]
    public async Task<IActionResult> PayExpenseFromFund(Guid expenseId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetAccessToken();

            // Get expense
            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == expenseId);

            if (expense == null)
            {
                return NotFound(new { message = "Expense not found" });
            }

            // Verify user has access to the group
            var hasAccess = await _groupServiceClient.IsUserInGroupAsync(expense.GroupId, userId, accessToken);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted to pay expense {ExpenseId} without access", userId, expenseId);
                return Forbidden(new { message = "Access denied to this group" });
            }

            // Check if fund has sufficient balance
            var hasBalance = await _fundServiceClient.HasSufficientBalanceAsync(expense.GroupId, expense.Amount, accessToken);
            if (!hasBalance)
            {
                return BadRequest(new { message = "Insufficient fund balance to pay this expense" });
            }

            // Pay expense from fund via Group Service
            var fundTransaction = await _fundServiceClient.PayExpenseFromFundAsync(
                expense.GroupId,
                expenseId,
                expense.Amount,
                $"Payment for expense: {expense.Description}",
                userId,
                accessToken);

            if (fundTransaction == null)
            {
                return StatusCode(500, new { message = "Failed to process fund payment" });
            }

            // Mark all invoices for this expense as paid
            var invoices = await _context.Invoices
                .Where(i => i.ExpenseId == expenseId)
                .ToListAsync();

            foreach (var invoice in invoices)
            {
                invoice.Status = InvoiceStatus.Paid;
                invoice.PaidAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Expense {ExpenseId} paid from fund for group {GroupId}. Fund transaction: {FundTransactionId}",
                expenseId, expense.GroupId, fundTransaction.Id);

            return Ok(new
            {
                message = "Expense paid successfully from group fund",
                expenseId = expenseId,
                fundTransaction = fundTransaction
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error paying expense {ExpenseId} from fund", expenseId);
            return StatusCode(500, new { message = "An error occurred while paying expense from fund" });
        }
    }

    /// <summary>
    /// Get all payments (admin/staff only)
    /// Note: Payer information is not included as User entity is in User service (microservices architecture)
    /// </summary>
    [HttpGet("payments")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> GetAllPayments([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] PaymentStatus? status = null)
    {
        try
        {
            // Query payments without including ignored navigation properties (Payer, Expense.Group, etc.)
            // These are stored in other microservices and must be fetched via HTTP if needed
            var query = _context.Payments
                .Include(p => p.Invoice)
                    .ThenInclude(i => i.Expense) // Expense is OK - it's in Payment service
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
                    PayerId = p.PayerId, // Only return ID - fetch user details via User service if needed
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
    /// Internal endpoint for service-to-service communication (Analytics, Admin, etc.)
    /// Allows services to fetch payments without user context
    /// Note: This endpoint is used by background services that don't have HttpContext
    /// </summary>
    [HttpGet("internal/payments")]
    [AllowAnonymous] // Allow service-to-service calls without JWT (internal network only)
    public async Task<IActionResult> GetPaymentsInternal([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] PaymentStatus? status = null)
    {
        try
        {
            // Query payments without including ignored navigation properties (Payer, Expense.Group, etc.)
            // These are stored in other microservices and must be fetched via HTTP if needed
            var query = _context.Payments
                .Include(p => p.Invoice)
                    .ThenInclude(i => i.Expense) // Expense is OK - it's in Payment service
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
                    PayerId = p.PayerId, // Only return ID - fetch user details via User service if needed
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
            _logger.LogError(ex, "Error getting payments (internal)");
            return StatusCode(500, new { message = "An error occurred while retrieving payments" });
        }
    }

    /// <summary>
    /// Internal endpoint for service-to-service communication to get expenses
    /// Allows services to fetch expenses without user context
    /// Note: This endpoint is used by background services that don't have HttpContext
    /// </summary>
    [HttpGet("internal/expenses")]
    [AllowAnonymous] // Allow service-to-service calls without JWT (internal network only)
    public async Task<IActionResult> GetExpensesInternal([FromQuery] Guid? groupId = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        try
        {
            // Query expenses - no user filtering for internal calls
            var query = _context.Expenses.AsQueryable();

            if (groupId.HasValue)
                query = query.Where(e => e.GroupId == groupId.Value);

            if (from.HasValue)
                query = query.Where(e => e.DateIncurred >= from.Value);
            if (to.HasValue)
                query = query.Where(e => e.DateIncurred <= to.Value);

            var expenses = await query
                .OrderByDescending(e => e.DateIncurred)
                .ToListAsync();

            // Map expenses to DTOs (simplified - no user/group details for internal calls)
            var expenseDtos = expenses.Select(e => new ExpenseDto
            {
                Id = e.Id,
                GroupId = e.GroupId,
                GroupName = null, // Not fetched for internal calls
                VehicleId = e.VehicleId,
                VehicleModel = null,
                ExpenseType = (ExpenseType)e.ExpenseType,
                Amount = e.Amount,
                Description = e.Description,
                DateIncurred = e.DateIncurred,
                CreatedBy = e.CreatedBy,
                CreatedByName = null, // Not fetched for internal calls
                Notes = e.Notes,
                IsRecurring = e.IsRecurring,
                CreatedAt = e.CreatedAt
            }).ToList();

            return Ok(expenseDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses (internal)");
            return StatusCode(500, new { message = "An error occurred while retrieving expenses" });
        }
    }

    /// <summary>
    /// Get expenses for user's groups
    /// Note: Fetches group and user data via HTTP calls since these entities are in other microservices
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
            var accessToken = GetAccessToken();

            // First, get user's groups from Group Service to filter expenses
            // We need to check which groups the user belongs to since we can't query Group.Members here
            var userGroups = await _groupServiceClient.GetUserGroups(accessToken);
            var userGroupIds = userGroups.Select(g => g.Id).ToList();

            if (!userGroupIds.Any())
            {
                return Ok(new List<ExpenseDto>()); // User is not in any groups
            }

            // Query expenses - only use foreign key IDs, no navigation properties
            var query = _context.Expenses
                .Where(e => userGroupIds.Contains(e.GroupId));

            if (groupId.HasValue)
            {
                // Verify user has access to this specific group
                if (!userGroupIds.Contains(groupId.Value))
                {
                    return Forbidden(new { message = "Access denied to this group" });
                }
                query = query.Where(e => e.GroupId == groupId.Value);
            }

            var expenses = await query
                .OrderByDescending(e => e.DateIncurred)
                .ToListAsync();

            // Fetch group and user data via HTTP calls
            var groupIds = expenses.Select(e => e.GroupId).Distinct().ToList();
            var userIds = expenses.Select(e => e.CreatedBy).Distinct().ToList();

            var groupsDict = new Dictionary<Guid, GroupDetailsDto>();
            var usersDict = await _userServiceClient.GetUsersAsync(userIds, accessToken);

            // Fetch group details for each unique group
            foreach (var groupIdValue in groupIds)
            {
                var groupDetails = await _groupServiceClient.GetGroupDetailsAsync(groupIdValue, accessToken);
                if (groupDetails != null)
                {
                    groupsDict[groupIdValue] = groupDetails;
                }
            }

            // Map expenses to DTOs with fetched data
            var expenseDtos = expenses.Select(e =>
            {
                var group = groupsDict.GetValueOrDefault(e.GroupId);
                var creator = usersDict.GetValueOrDefault(e.CreatedBy);

                return new ExpenseDto
                {
                    Id = e.Id,
                    GroupId = e.GroupId,
                    GroupName = group?.Name ?? "Unknown Group",
                    VehicleId = e.VehicleId,
                    VehicleModel = null, // Vehicle details would need VehicleServiceClient if needed
                    ExpenseType = (ExpenseType)e.ExpenseType,
                    Amount = e.Amount,
                    Description = e.Description,
                    DateIncurred = e.DateIncurred,
                    CreatedBy = e.CreatedBy,
                    CreatedByName = creator != null ? $"{creator.FirstName} {creator.LastName}" : "Unknown User",
                    Notes = e.Notes,
                    IsRecurring = e.IsRecurring,
                    CreatedAt = e.CreatedAt
                };
            }).ToList();

            return Ok(expenseDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses");
            return StatusCode(500, new { message = "An error occurred while retrieving expenses" });
        }
    }

    /// <summary>
    /// Get invoices for user
    /// Note: Fetches expense, group, and user data via HTTP calls since these entities are in other microservices
    /// </summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] Guid? groupId = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetAccessToken();

            // Query invoices - only use foreign key IDs, no ignored navigation properties
            var query = _context.Invoices
                .Include(i => i.Expense) // Expense is OK - it's in Payment service
                .Where(i => i.PayerId == userId);

            if (groupId.HasValue)
            {
                query = query.Where(i => i.Expense.GroupId == groupId.Value);
            }

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            // Fetch related data via HTTP calls
            var groupIds = invoices.Select(i => i.Expense.GroupId).Distinct().ToList();
            var payerIds = invoices.Select(i => i.PayerId).Distinct().ToList();

            var groupsDict = new Dictionary<Guid, GroupDetailsDto>();
            var usersDict = await _userServiceClient.GetUsersAsync(payerIds, accessToken);

            // Fetch group details for each unique group
            foreach (var groupIdValue in groupIds)
            {
                var groupDetails = await _groupServiceClient.GetGroupDetailsAsync(groupIdValue, accessToken);
                if (groupDetails != null)
                {
                    groupsDict[groupIdValue] = groupDetails;
                }
            }

            // Map invoices to DTOs with fetched data
            var invoiceDtos = invoices.Select(i =>
            {
                var payer = usersDict.GetValueOrDefault(i.PayerId);
                var group = groupsDict.GetValueOrDefault(i.Expense.GroupId);

                return new InvoiceDto
                {
                    Id = i.Id,
                    ExpenseId = i.ExpenseId,
                    PayerId = i.PayerId,
                    PayerName = payer != null ? $"{payer.FirstName} {payer.LastName}" : "Unknown User",
                    Amount = i.Amount,
                    InvoiceNumber = i.InvoiceNumber,
                    Status = (InvoiceStatus)i.Status,
                    DueDate = i.DueDate,
                    PaidAt = i.PaidAt,
                    CreatedAt = i.CreatedAt,
                    Expense = new ExpenseDto
                    {
                        Id = i.Expense.Id,
                        GroupId = i.Expense.GroupId,
                        GroupName = group?.Name ?? "Unknown Group",
                        ExpenseType = (ExpenseType)i.Expense.ExpenseType,
                        Description = i.Expense.Description,
                        Amount = i.Expense.Amount,
                        DateIncurred = i.Expense.DateIncurred
                    }
                };
            }).ToList();

            return Ok(invoiceDtos);
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
                .FirstOrDefaultAsync(p => p.TransactionReference == response.OrderId);

            if (payment == null)
            {
                _logger.LogError("Payment not found for VNPay order {OrderId}", response.OrderId);
                return NotFound(new { message = "Payment record not found" });
            }

            // Check if this is a fund deposit payment (order ID starts with "FUND_")
            if (response.OrderId.StartsWith("FUND_"))
            {
                // Handle fund deposit payment
                var orderParts = response.OrderId.Split('_');
                if (orderParts.Length >= 2 && Guid.TryParse(orderParts[1], out var groupId))
                {
                    // Complete fund deposit via Group Service
                    // Note: We need to get access token, but callback is anonymous
                    // We'll need to store userId in payment notes or use a different approach
                    // For now, we'll extract userId from payment record
                    var userId = payment.PayerId;
                    
                    // Get access token from payment notes or use a service account approach
                    // Since callback is anonymous, we'll need to handle this differently
                    // For now, we'll call Group service without auth (it should validate internally)
                    // Actually, we need to get the access token somehow - let's store it in payment notes temporarily
                    // Or better: Group service endpoint should accept payment reference validation
                    
                    // Call Group service to complete deposit
                    // We'll need to pass the payment reference for validation
                    var fundTransaction = await _fundServiceClient.CompleteFundDepositAsync(
                        groupId,
                        response.Amount,
                        payment.Notes ?? "Nạp quỹ qua VNPay",
                        response.TransactionId,
                        userId,
                        response.OrderId,
                        string.Empty // No access token in callback - Group service should validate payment reference
                    );

                    if (fundTransaction == null)
                    {
                        _logger.LogError("Failed to complete fund deposit for group {GroupId} after VNPay payment", groupId);
                        // Still update payment status as completed since VNPay payment succeeded
                        payment.Status = Domain.Entities.PaymentStatus.Completed;
                        payment.TransactionReference = response.TransactionId;
                        payment.PaidAt = response.PayDate;
                        payment.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        
                        return BadRequest(new { message = "Fund deposit completion failed", response });
                    }

                    // Update payment status
                    payment.Status = Domain.Entities.PaymentStatus.Completed;
                    payment.TransactionReference = response.TransactionId;
                    payment.PaidAt = response.PayDate;
                    payment.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("VNPay fund deposit completed for group {GroupId}, order {OrderId}, amount {Amount} VND", 
                        groupId, response.OrderId, response.Amount);

                    // Redirect to frontend callback page
                    // Get frontend URL from configuration or use default
                    var frontendBaseUrl = _configuration["FrontendBaseUrl"] ?? "http://localhost:5173";
                    var frontendCallbackUrl = $"{frontendBaseUrl}/payment/callback?success=true&type=fund&groupId={groupId}&orderId={response.OrderId}&amount={response.Amount}";
                    return Redirect(frontendCallbackUrl);
                }
                else
                {
                    _logger.LogError("Invalid fund deposit order ID format: {OrderId}", response.OrderId);
                    return BadRequest(new { message = "Invalid order ID format" });
                }
            }
            else
            {
                // Handle invoice payment (existing flow)
                // Note: Only include Expense (which is in Payment service), not Group (which is ignored)
                var paymentWithInvoice = await _context.Payments
                    .Include(p => p.Invoice)
                        .ThenInclude(i => i.Expense) // Expense is OK - it's in Payment service
                    .FirstOrDefaultAsync(p => p.Id == payment.Id);

                if (paymentWithInvoice?.Invoice == null)
                {
                    _logger.LogError("Invoice not found for payment {PaymentId}", payment.Id);
                    return NotFound(new { message = "Invoice not found" });
                }

                // Update payment status
                payment.Status = Domain.Entities.PaymentStatus.Completed;
                payment.TransactionReference = response.TransactionId;
                payment.PaidAt = response.PayDate;
                payment.UpdatedAt = DateTime.UtcNow;

                // Update invoice status
                paymentWithInvoice.Invoice.Status = Domain.Entities.InvoiceStatus.Paid;
                paymentWithInvoice.Invoice.PaidAt = response.PayDate;

                await _context.SaveChangesAsync();

                // Publish payment settled event (only for invoice payments)
                if (paymentWithInvoice.InvoiceId.HasValue)
                {
                    await _publishEndpoint.Publish(new PaymentSettledEvent
                    {
                        PaymentId = payment.Id,
                        InvoiceId = paymentWithInvoice.InvoiceId.Value,
                        ExpenseId = paymentWithInvoice.Invoice.ExpenseId,
                        PayerId = payment.PayerId,
                        Amount = payment.Amount,
                        Method = PaymentMethod.EWallet,
                        TransactionReference = response.TransactionId,
                        PaidAt = response.PayDate
                    });
                }

                _logger.LogInformation("VNPay payment completed for order {OrderId}, amount {Amount} VND", 
                    response.OrderId, response.Amount);

                // Redirect to frontend callback page
                var frontendBaseUrl = _configuration["FrontendBaseUrl"] ?? "http://localhost:5173";
                var frontendCallbackUrl = paymentWithInvoice.InvoiceId.HasValue
                    ? $"{frontendBaseUrl}/payment/callback?success=true&type=invoice&invoiceId={paymentWithInvoice.InvoiceId.Value}&orderId={response.OrderId}"
                    : $"{frontendBaseUrl}/payment/callback?success=true&type=payment&orderId={response.OrderId}";
                return Redirect(frontendCallbackUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay callback");
            return StatusCode(500, new { message = "An error occurred while processing payment" });
        }
    }

    /// <summary>
    /// Create VNPay payment URL for fund deposit
    /// </summary>
    [HttpPost("fund/{groupId:guid}/deposit-vnpay")]
    public async Task<IActionResult> CreateFundDepositVnPayPayment(Guid groupId, [FromBody] CreateFundDepositPaymentDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var accessToken = GetAccessToken();

            // Verify user has access to the group
            var hasAccess = await _groupServiceClient.IsUserInGroupAsync(groupId, userId, accessToken);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted to create fund deposit payment for group {GroupId} without access", userId, groupId);
                return Forbidden(new { message = "Access denied to this group" });
            }

            // Create VNPay payment request
            var orderId = $"FUND_{groupId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            var paymentRequest = new VnPayPaymentRequest
            {
                OrderId = orderId,
                Amount = createDto.Amount,
                OrderInfo = $"Nạp quỹ nhóm: {createDto.Description}",
                OrderType = "fund_deposit",
                IpAddress = GetClientIpAddress(),
                BankCode = null
            };

            var paymentUrl = _vnPayService.CreatePaymentUrl(paymentRequest);

            // Create payment record for fund deposit
            // Note: We don't have an Invoice for fund deposits, so InvoiceId is null
            var payment = new Domain.Entities.Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = null, // No invoice for fund deposits
                PayerId = userId,
                Amount = createDto.Amount,
                Method = Domain.Entities.PaymentMethod.EWallet, // VNPay is e-wallet
                Status = Domain.Entities.PaymentStatus.Pending,
                TransactionReference = orderId,
                Notes = $"Fund deposit for group {groupId}: {createDto.Description}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("VNPay fund deposit payment created for group {GroupId}, order {OrderId}", 
                groupId, orderId);

            return Ok(new FundDepositPaymentResponse
            {
                PaymentUrl = paymentUrl,
                OrderId = orderId,
                Amount = createDto.Amount,
                PaymentId = payment.Id,
                GroupId = groupId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VNPay fund deposit payment");
            return StatusCode(500, new { message = "An error occurred while creating payment" });
        }
    }

    /// <summary>
    /// Create cost splitting invoices for all group members based on their ownership percentages
    /// Note: Fetches group members via HTTP call since Group/Member entities are in Group service
    /// </summary>
    private async Task CreateCostSplittingInvoicesAsync(Guid expenseId)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null)
        {
            _logger.LogWarning("Expense {ExpenseId} not found when creating cost splitting invoices", expenseId);
            return;
        }

        // Fetch group details including members from Group Service
        var accessToken = GetAccessToken();
        var groupDetails = await _groupServiceClient.GetGroupDetailsAsync(expense.GroupId, accessToken);

        if (groupDetails == null || !groupDetails.Members.Any())
        {
            _logger.LogWarning("Group {GroupId} not found or has no members when creating invoices for expense {ExpenseId}", 
                expense.GroupId, expenseId);
            return;
        }

        var invoiceNumber = 1;

        foreach (var member in groupDetails.Members)
        {
            // Calculate member's share of the expense based on ownership percentage
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
        _logger.LogInformation("Created {Count} invoices for expense {ExpenseId} split among {MemberCount} members", 
            invoiceNumber - 1, expenseId, groupDetails.Members.Count);
    }

    /// <summary>
    /// Get expense by ID with related data fetched via HTTP calls
    /// Note: Fetches group and user data via HTTP since these entities are in other microservices
    /// </summary>
    private async Task<ExpenseDto> GetExpenseByIdAsync(Guid expenseId)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId);

        if (expense == null)
        {
            throw new InvalidOperationException($"Expense {expenseId} not found");
        }

        // Fetch related data via HTTP calls
        var accessToken = GetAccessToken();
        var groupDetails = await _groupServiceClient.GetGroupDetailsAsync(expense.GroupId, accessToken);
        var creator = await _userServiceClient.GetUserAsync(expense.CreatedBy, accessToken);

        return new ExpenseDto
        {
            Id = expense.Id,
            GroupId = expense.GroupId,
            GroupName = groupDetails?.Name ?? "Unknown Group",
            VehicleId = expense.VehicleId,
            VehicleModel = null, // Vehicle details would need VehicleServiceClient if needed
            ExpenseType = (ExpenseType)expense.ExpenseType,
            Amount = expense.Amount,
            Description = expense.Description,
            DateIncurred = expense.DateIncurred,
            CreatedBy = expense.CreatedBy,
            CreatedByName = creator != null ? $"{creator.FirstName} {creator.LastName}" : "Unknown User",
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

    private string GetAccessToken()
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return string.Empty;
        }
        return authHeader.Substring("Bearer ".Length).Trim();
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

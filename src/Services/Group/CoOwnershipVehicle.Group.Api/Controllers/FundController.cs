using CoOwnershipVehicle.Group.Api.Contracts;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FundController : ControllerBase
{
    private readonly IFundService _fundService;
    private readonly ILogger<FundController> _logger;

    public FundController(
        IFundService fundService,
        ILogger<FundController> logger)
    {
        _fundService = fundService ?? throw new ArgumentNullException(nameof(fundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get fund balance and summary for a group
    /// </summary>
    [HttpGet("{groupId:guid}")]
    [ProducesResponseType(typeof(FundBalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundBalanceDto>> GetFundBalance(Guid groupId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var balance = await _fundService.GetFundBalanceAsync(groupId, userId);
            return Ok(balance);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get fund balance for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fund balance for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving fund balance" });
        }
    }

    /// <summary>
    /// Deposit funds to group account
    /// </summary>
    [HttpPost("{groupId:guid}/deposit")]
    [ProducesResponseType(typeof(FundTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundTransactionDto>> DepositFund(Guid groupId, [FromBody] DepositFundDto depositDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var transaction = await _fundService.DepositFundAsync(groupId, depositDto, userId);
            return Ok(transaction);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to deposit funds to group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid deposit request for group {GroupId}", groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error depositing funds to group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while depositing funds" });
        }
    }

    /// <summary>
    /// Withdraw funds from group account
    /// </summary>
    [HttpPost("{groupId:guid}/withdraw")]
    [ProducesResponseType(typeof(FundTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundTransactionDto>> WithdrawFund(Guid groupId, [FromBody] WithdrawFundDto withdrawDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var transaction = await _fundService.WithdrawFundAsync(groupId, withdrawDto, userId);
            return Ok(transaction);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to withdraw funds from group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid withdrawal request for group {GroupId}", groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing funds from group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while withdrawing funds" });
        }
    }

    /// <summary>
    /// Allocate funds to reserve
    /// </summary>
    [HttpPost("{groupId:guid}/allocate-reserve")]
    [ProducesResponseType(typeof(FundTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundTransactionDto>> AllocateReserve(Guid groupId, [FromBody] AllocateReserveDto allocateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var transaction = await _fundService.AllocateReserveAsync(groupId, allocateDto, userId);
            return Ok(transaction);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to allocate reserve for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid reserve allocation request for group {GroupId}", groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating reserve for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while allocating reserve" });
        }
    }

    /// <summary>
    /// Release funds from reserve
    /// </summary>
    [HttpPost("{groupId:guid}/release-reserve")]
    [ProducesResponseType(typeof(FundTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundTransactionDto>> ReleaseReserve(Guid groupId, [FromBody] ReleaseReserveDto releaseDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var transaction = await _fundService.ReleaseReserveAsync(groupId, releaseDto, userId);
            return Ok(transaction);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to release reserve for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid reserve release request for group {GroupId}", groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing reserve for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while releasing reserve" });
        }
    }

    /// <summary>
    /// Get transaction history
    /// </summary>
    [HttpGet("{groupId:guid}/transactions")]
    [ProducesResponseType(typeof(FundTransactionHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundTransactionHistoryDto>> GetTransactionHistory(
        Guid groupId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] FundTransactionType? type = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var history = await _fundService.GetTransactionHistoryAsync(groupId, userId, page, pageSize, type, fromDate, toDate);
            return Ok(history);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get transaction history for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transaction history for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving transaction history" });
        }
    }

    /// <summary>
    /// Get fund summary for a period
    /// </summary>
    [HttpGet("{groupId:guid}/summary/{period}")]
    [ProducesResponseType(typeof(FundSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundSummaryDto>> GetFundSummary(Guid groupId, string period)
    {
        try
        {
            var userId = GetCurrentUserId();
            var summary = await _fundService.GetFundSummaryAsync(groupId, userId, period);
            return Ok(summary);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get fund summary for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid period for fund summary: {Period}", period);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fund summary for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving fund summary" });
        }
    }

    /// <summary>
    /// Approve a pending withdrawal request
    /// </summary>
    [HttpPost("{groupId:guid}/withdrawals/{transactionId:guid}/approve")]
    [ProducesResponseType(typeof(FundTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundTransactionDto>> ApproveWithdrawal(Guid groupId, Guid transactionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var transaction = await _fundService.ApproveWithdrawalAsync(groupId, transactionId, userId);
            return Ok(transaction);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to approve withdrawal {TransactionId} for group {GroupId}", transactionId, groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid approval request for withdrawal {TransactionId} in group {GroupId}", transactionId, groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving withdrawal {TransactionId} for group {GroupId}", transactionId, groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while approving withdrawal" });
        }
    }

    /// <summary>
    /// Reject a pending withdrawal request
    /// </summary>
    [HttpPost("{groupId:guid}/withdrawals/{transactionId:guid}/reject")]
    [ProducesResponseType(typeof(FundTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundTransactionDto>> RejectWithdrawal(Guid groupId, Guid transactionId, [FromBody] RejectWithdrawalDto? rejectDto = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var transaction = await _fundService.RejectWithdrawalAsync(groupId, transactionId, userId, rejectDto?.Reason);
            return Ok(transaction);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to reject withdrawal {TransactionId} for group {GroupId}", transactionId, groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid rejection request for withdrawal {TransactionId} in group {GroupId}", transactionId, groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting withdrawal {TransactionId} for group {GroupId}", transactionId, groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while rejecting withdrawal" });
        }
    }

    /// <summary>
    /// Complete fund deposit after VNPay payment
    /// Called by Payment service after successful VNPay payment callback
    /// Note: This endpoint allows service-to-service calls without user authentication
    /// Payment reference validation ensures security
    /// </summary>
    [HttpPost("{groupId:guid}/complete-deposit")]
    [AllowAnonymous] // Allow Payment service to call without user token
    [ProducesResponseType(typeof(FundTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundTransactionDto>> CompleteDepositFromPayment(Guid groupId, [FromBody] CompleteFundDepositDto completeDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validate groupId matches
            if (completeDto.GroupId != groupId)
            {
                return BadRequest(new { message = "Group ID mismatch" });
            }

            var transaction = await _fundService.CompleteDepositFromPaymentAsync(
                groupId,
                completeDto.Amount,
                completeDto.Description,
                completeDto.PaymentReference,
                completeDto.InitiatedBy,
                completeDto.Reference);
            
            return Ok(transaction);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to complete fund deposit for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid fund deposit completion request for group {GroupId}", groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid fund deposit completion request for group {GroupId}", groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing fund deposit for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while completing fund deposit" });
        }
    }

    /// <summary>
    /// Pay an expense from group fund
    /// Called by Payment service when an expense is paid using group fund
    /// </summary>
    [HttpPost("{groupId:guid}/pay-expense")]
    [ProducesResponseType(typeof(FundTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FundTransactionDto>> PayExpenseFromFund(Guid groupId, [FromBody] PayExpenseFromFundDto payDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var transaction = await _fundService.PayExpenseFromFundAsync(
                groupId, 
                payDto.ExpenseId, 
                payDto.Amount, 
                payDto.Description, 
                userId);
            return Ok(transaction);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to pay expense from fund for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid expense payment request for group {GroupId}", groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid expense payment request for group {GroupId}", groupId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error paying expense from fund for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while paying expense from fund" });
        }
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
}


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


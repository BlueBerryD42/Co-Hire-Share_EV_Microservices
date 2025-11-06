using CoOwnershipVehicle.Group.Api.Contracts;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProposalController : ControllerBase
{
    private readonly IProposalService _proposalService;
    private readonly ILogger<ProposalController> _logger;

    public ProposalController(
        IProposalService proposalService,
        ILogger<ProposalController> logger)
    {
        _proposalService = proposalService ?? throw new ArgumentNullException(nameof(proposalService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new proposal
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProposalDto>> CreateProposal([FromBody] CreateProposalDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var proposal = await _proposalService.CreateProposalAsync(createDto, userId);
            return Ok(proposal);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to create proposal");
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Group not found for proposal creation");
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid proposal data");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating proposal");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating proposal" });
        }
    }

    /// <summary>
    /// Get all proposals for a group
    /// </summary>
    [HttpGet("group/{groupId:guid}")]
    [ProducesResponseType(typeof(List<ProposalListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ProposalListDto>>> GetProposalsByGroup(
        Guid groupId,
        [FromQuery] ProposalStatus? status = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var proposals = await _proposalService.GetProposalsByGroupAsync(groupId, userId, status);
            return Ok(proposals);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get proposals");
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving proposals for group {GroupId}", groupId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving proposals" });
        }
    }

    /// <summary>
    /// Get proposal details
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProposalDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProposalDetailsDto>> GetProposal(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var proposal = await _proposalService.GetProposalByIdAsync(id, userId);
            return Ok(proposal);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Proposal {ProposalId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get proposal {ProposalId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving proposal {ProposalId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving proposal" });
        }
    }

    /// <summary>
    /// Cast a vote on a proposal
    /// </summary>
    [HttpPost("{id:guid}/vote")]
    [ProducesResponseType(typeof(VoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VoteDto>> CastVote(Guid id, [FromBody] CastVoteDto voteDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var vote = await _proposalService.CastVoteAsync(id, voteDto, userId);
            return Ok(vote);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Proposal {ProposalId} not found for voting", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to vote on proposal {ProposalId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when voting on proposal {ProposalId}", id);
            if (ex.Message.Contains("already voted"))
            {
                return StatusCode(StatusCodes.Status409Conflict, new { message = ex.Message });
            }
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error casting vote on proposal {ProposalId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while casting vote" });
        }
    }

    /// <summary>
    /// Get voting results for a proposal
    /// </summary>
    [HttpGet("{id:guid}/results")]
    [ProducesResponseType(typeof(ProposalResultsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProposalResultsDto>> GetProposalResults(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var results = await _proposalService.GetProposalResultsAsync(id, userId);
            return Ok(results);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Proposal {ProposalId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get proposal results {ProposalId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving proposal results {ProposalId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving proposal results" });
        }
    }

    /// <summary>
    /// Close voting on a proposal
    /// </summary>
    [HttpPut("{id:guid}/close")]
    [ProducesResponseType(typeof(ProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProposalDto>> CloseProposal(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var proposal = await _proposalService.CloseProposalAsync(id, userId);
            return Ok(proposal);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Proposal {ProposalId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to close proposal {ProposalId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when closing proposal {ProposalId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing proposal {ProposalId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while closing proposal" });
        }
    }

    /// <summary>
    /// Cancel a proposal
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelProposal(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _proposalService.CancelProposalAsync(id, userId);
            return Ok(new { message = "Proposal cancelled successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Proposal {ProposalId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to cancel proposal {ProposalId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when cancelling proposal {ProposalId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling proposal {ProposalId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while cancelling proposal" });
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


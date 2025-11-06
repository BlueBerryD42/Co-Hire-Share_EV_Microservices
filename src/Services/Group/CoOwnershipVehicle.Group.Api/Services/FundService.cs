using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Contracts;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Group.Api.Services;

public class FundService : IFundService
{
    private const decimal LargeWithdrawalThreshold = 1000m; // Configurable threshold
    private readonly GroupDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FundService> _logger;

    public FundService(
        GroupDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<FundService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FundBalanceDto> GetFundBalanceAsync(Guid groupId, Guid userId)
    {
        // Validate user is member
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        // Get or create fund
        var fund = await _context.GroupFunds
            .FirstOrDefaultAsync(f => f.GroupId == groupId);

        if (fund == null)
        {
            fund = new GroupFund
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                TotalBalance = 0m,
                ReserveBalance = 0m,
                LastUpdated = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.GroupFunds.Add(fund);
            await _context.SaveChangesAsync();
        }

        // Get recent transactions
        var recentTransactions = await _context.FundTransactions
            .Include(t => t.Initiator)
            .Include(t => t.Approver)
            .Where(t => t.GroupId == groupId)
            .OrderByDescending(t => t.TransactionDate)
            .Take(10)
            .ToListAsync();

        // Calculate statistics
        var allTransactions = await _context.FundTransactions
            .Where(t => t.GroupId == groupId && t.Status == FundTransactionStatus.Completed)
            .ToListAsync();

        var totalDeposits = allTransactions
            .Where(t => t.Type == FundTransactionType.Deposit)
            .Sum(t => t.Amount);

        var totalWithdrawals = allTransactions
            .Where(t => t.Type == FundTransactionType.Withdrawal)
            .Sum(t => t.Amount);

        var memberContributions = await _context.FundTransactions
            .Include(t => t.Initiator)
            .Where(t => t.GroupId == groupId && 
                       t.Type == FundTransactionType.Deposit && 
                       t.Status == FundTransactionStatus.Completed)
            .GroupBy(t => t.InitiatedBy)
            .Select(g => new
            {
                UserId = g.Key,
                UserName = g.First().Initiator.FirstName + " " + g.First().Initiator.LastName,
                Total = g.Sum(t => t.Amount)
            })
            .ToDictionaryAsync(x => x.UserName, x => x.Total);

        return new FundBalanceDto
        {
            GroupId = fund.GroupId,
            TotalBalance = fund.TotalBalance,
            ReserveBalance = fund.ReserveBalance,
            AvailableBalance = fund.AvailableBalance,
            LastUpdated = fund.LastUpdated,
            RecentTransactions = recentTransactions.Select(t => MapToDto(t, t.Initiator, t.Approver)).ToList(),
            Statistics = new FundStatisticsDto
            {
                TotalDeposits = totalDeposits,
                TotalWithdrawals = totalWithdrawals,
                NetChange = totalDeposits - totalWithdrawals,
                MemberContributions = memberContributions
            }
        };
    }

    public async Task<FundTransactionDto> DepositFundAsync(Guid groupId, DepositFundDto depositDto, Guid userId)
    {
        // Validate user is member
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        if (depositDto.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than 0");
        }

        // Get or create fund
        var fund = await _context.GroupFunds
            .FirstOrDefaultAsync(f => f.GroupId == groupId);

        if (fund == null)
        {
            fund = new GroupFund
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                TotalBalance = 0m,
                ReserveBalance = 0m,
                LastUpdated = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.GroupFunds.Add(fund);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var balanceBefore = fund.TotalBalance;
            fund.TotalBalance += depositDto.Amount;

            // Auto-allocate to reserve if specified
            if (depositDto.AutoAllocateToReservePercent.HasValue && depositDto.AutoAllocateToReservePercent.Value > 0)
            {
                var reserveAmount = depositDto.Amount * (depositDto.AutoAllocateToReservePercent.Value / 100m);
                fund.ReserveBalance += reserveAmount;
            }

            fund.LastUpdated = DateTime.UtcNow;
            var balanceAfter = fund.TotalBalance;

            var fundTransaction = new FundTransaction
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                InitiatedBy = userId,
                Type = FundTransactionType.Deposit,
                Amount = depositDto.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                Description = depositDto.Description,
                Status = FundTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow,
                Reference = depositDto.Reference,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.FundTransactions.Add(fundTransaction);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Fund deposit of {Amount} made to group {GroupId} by user {UserId}",
                depositDto.Amount, groupId, userId);

            // Publish event
            await _publishEndpoint.Publish(new FundDepositEvent
            {
                TransactionId = fundTransaction.Id,
                GroupId = groupId,
                DepositedBy = userId,
                Amount = depositDto.Amount,
                BalanceAfter = balanceAfter,
                Description = depositDto.Description,
                DepositedAt = DateTime.UtcNow
            });

            var initiator = await _context.Users.FindAsync(userId);
            return MapToDto(fundTransaction, initiator, null);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<FundTransactionDto> WithdrawFundAsync(Guid groupId, WithdrawFundDto withdrawDto, Guid userId)
    {
        // Validate user is admin
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        if (membership.RoleInGroup != GroupRole.Admin)
        {
            throw new UnauthorizedAccessException("Only group admins can withdraw funds");
        }

        if (withdrawDto.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than 0");
        }

        var fund = await _context.GroupFunds
            .FirstOrDefaultAsync(f => f.GroupId == groupId);

        if (fund == null)
        {
            throw new InvalidOperationException("Fund not found for this group");
        }

        if (fund.AvailableBalance < withdrawDto.Amount)
        {
            throw new InvalidOperationException("Insufficient available balance");
        }

        var balanceBefore = fund.TotalBalance;
        var status = withdrawDto.Amount >= LargeWithdrawalThreshold
            ? FundTransactionStatus.Pending
            : FundTransactionStatus.Approved;

        var fundTransaction = new FundTransaction
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            InitiatedBy = userId,
            Type = FundTransactionType.Withdrawal,
            Amount = withdrawDto.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceBefore - withdrawDto.Amount,
            Description = withdrawDto.Reason,
            Status = status,
            TransactionDate = DateTime.UtcNow,
            Reference = withdrawDto.Recipient,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.FundTransactions.Add(fundTransaction);

        // If auto-approved, update balance immediately
        if (status == FundTransactionStatus.Approved)
        {
            fund.TotalBalance -= withdrawDto.Amount;
            fund.LastUpdated = DateTime.UtcNow;
            fundTransaction.Status = FundTransactionStatus.Completed;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Fund withdrawal of {Amount} requested for group {GroupId} by user {UserId}, status: {Status}",
            withdrawDto.Amount, groupId, userId, status);

        // Publish event
        await _publishEndpoint.Publish(new FundWithdrawalEvent
        {
            TransactionId = fundTransaction.Id,
            GroupId = groupId,
            WithdrawnBy = userId,
            Amount = withdrawDto.Amount,
            BalanceAfter = fundTransaction.BalanceAfter,
            Reason = withdrawDto.Reason,
            Status = status,
            WithdrawnAt = DateTime.UtcNow
        });

        var initiator = await _context.Users.FindAsync(userId);
        return MapToDto(fundTransaction, initiator, null);
    }

    public async Task<FundTransactionDto> AllocateReserveAsync(Guid groupId, AllocateReserveDto allocateDto, Guid userId)
    {
        // Validate user is admin
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        if (membership.RoleInGroup != GroupRole.Admin)
        {
            throw new UnauthorizedAccessException("Only group admins can allocate reserves");
        }

        if (allocateDto.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than 0");
        }

        var fund = await _context.GroupFunds
            .FirstOrDefaultAsync(f => f.GroupId == groupId);

        if (fund == null)
        {
            throw new InvalidOperationException("Fund not found for this group");
        }

        if (fund.AvailableBalance < allocateDto.Amount)
        {
            throw new InvalidOperationException("Insufficient available balance");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var balanceBefore = fund.TotalBalance;
            fund.ReserveBalance += allocateDto.Amount;
            fund.LastUpdated = DateTime.UtcNow;

            var fundTransaction = new FundTransaction
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                InitiatedBy = userId,
                Type = FundTransactionType.Allocation,
                Amount = allocateDto.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceBefore, // Total balance doesn't change
                Description = allocateDto.Reason,
                Status = FundTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.FundTransactions.Add(fundTransaction);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Reserve allocation of {Amount} made for group {GroupId} by user {UserId}",
                allocateDto.Amount, groupId, userId);

            // Publish event
            await _publishEndpoint.Publish(new FundAllocationEvent
            {
                TransactionId = fundTransaction.Id,
                GroupId = groupId,
                AllocatedBy = userId,
                Amount = allocateDto.Amount,
                Type = FundTransactionType.Allocation,
                ReserveBalanceAfter = fund.ReserveBalance,
                AvailableBalanceAfter = fund.AvailableBalance,
                Reason = allocateDto.Reason,
                AllocatedAt = DateTime.UtcNow
            });

            var initiator = await _context.Users.FindAsync(userId);
            return MapToDto(fundTransaction, initiator, null);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<FundTransactionDto> ReleaseReserveAsync(Guid groupId, ReleaseReserveDto releaseDto, Guid userId)
    {
        // Validate user is admin
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        if (membership.RoleInGroup != GroupRole.Admin)
        {
            throw new UnauthorizedAccessException("Only group admins can release reserves");
        }

        if (releaseDto.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than 0");
        }

        var fund = await _context.GroupFunds
            .FirstOrDefaultAsync(f => f.GroupId == groupId);

        if (fund == null)
        {
            throw new InvalidOperationException("Fund not found for this group");
        }

        if (fund.ReserveBalance < releaseDto.Amount)
        {
            throw new InvalidOperationException("Insufficient reserve balance");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var balanceBefore = fund.TotalBalance;
            fund.ReserveBalance -= releaseDto.Amount;
            fund.LastUpdated = DateTime.UtcNow;

            var fundTransaction = new FundTransaction
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                InitiatedBy = userId,
                Type = FundTransactionType.Release,
                Amount = releaseDto.Amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceBefore, // Total balance doesn't change
                Description = releaseDto.Reason,
                Status = FundTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.FundTransactions.Add(fundTransaction);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Reserve release of {Amount} made for group {GroupId} by user {UserId}",
                releaseDto.Amount, groupId, userId);

            // Publish event
            await _publishEndpoint.Publish(new FundAllocationEvent
            {
                TransactionId = fundTransaction.Id,
                GroupId = groupId,
                AllocatedBy = userId,
                Amount = releaseDto.Amount,
                Type = FundTransactionType.Release,
                ReserveBalanceAfter = fund.ReserveBalance,
                AvailableBalanceAfter = fund.AvailableBalance,
                Reason = releaseDto.Reason,
                AllocatedAt = DateTime.UtcNow
            });

            var initiator = await _context.Users.FindAsync(userId);
            return MapToDto(fundTransaction, initiator, null);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<FundTransactionHistoryDto> GetTransactionHistoryAsync(
        Guid groupId, 
        Guid userId, 
        int page = 1, 
        int pageSize = 20, 
        FundTransactionType? type = null, 
        DateTime? fromDate = null, 
        DateTime? toDate = null)
    {
        // Validate user is member
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        var query = _context.FundTransactions
            .Include(t => t.Initiator)
            .Include(t => t.Approver)
            .Where(t => t.GroupId == groupId);

        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate <= toDate.Value);
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new FundTransactionHistoryDto
        {
            Transactions = transactions.Select(t => MapToDto(t, t.Initiator, t.Approver)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<FundSummaryDto> GetFundSummaryAsync(Guid groupId, Guid userId, string period)
    {
        // Validate user is member
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        var now = DateTime.UtcNow;
        var startDate = period.ToLower() switch
        {
            "monthly" => now.AddMonths(-1),
            "quarterly" => now.AddMonths(-3),
            "yearly" => now.AddYears(-1),
            _ => throw new ArgumentException("Period must be 'monthly', 'quarterly', or 'yearly'")
        };

        var transactions = await _context.FundTransactions
            .Include(t => t.Initiator)
            .Where(t => t.GroupId == groupId && 
                       t.TransactionDate >= startDate && 
                       t.Status == FundTransactionStatus.Completed)
            .ToListAsync();

        var totalDeposits = transactions
            .Where(t => t.Type == FundTransactionType.Deposit)
            .Sum(t => t.Amount);

        var totalWithdrawals = transactions
            .Where(t => t.Type == FundTransactionType.Withdrawal)
            .Sum(t => t.Amount);

        var reserveChanges = transactions
            .Where(t => t.Type == FundTransactionType.Allocation || t.Type == FundTransactionType.Release)
            .Sum(t => t.Type == FundTransactionType.Allocation ? t.Amount : -t.Amount);

        var memberContributions = transactions
            .Where(t => t.Type == FundTransactionType.Deposit)
            .GroupBy(t => t.InitiatedBy)
            .Select(g => new
            {
                UserId = g.Key,
                UserName = g.First().Initiator.FirstName + " " + g.First().Initiator.LastName,
                Total = g.Sum(t => t.Amount)
            })
            .ToDictionary(x => x.UserName, x => x.Total);

        // Calculate average balance (simplified - would need historical snapshots for accurate calculation)
        var fund = await _context.GroupFunds.FirstOrDefaultAsync(f => f.GroupId == groupId);
        var averageBalance = fund?.TotalBalance ?? 0m;

        return new FundSummaryDto
        {
            Period = period,
            TotalDeposits = totalDeposits,
            TotalWithdrawals = totalWithdrawals,
            NetChange = totalDeposits - totalWithdrawals,
            AverageBalance = averageBalance,
            MemberContributions = memberContributions,
            ReserveAllocationChanges = reserveChanges
        };
    }

    private static FundTransactionDto MapToDto(FundTransaction transaction, User? initiator, User? approver)
    {
        return new FundTransactionDto
        {
            Id = transaction.Id,
            GroupId = transaction.GroupId,
            InitiatedBy = transaction.InitiatedBy,
            InitiatorName = initiator != null ? $"{initiator.FirstName} {initiator.LastName}" : string.Empty,
            Type = transaction.Type,
            Amount = transaction.Amount,
            BalanceBefore = transaction.BalanceBefore,
            BalanceAfter = transaction.BalanceAfter,
            Description = transaction.Description,
            Status = transaction.Status,
            ApprovedBy = transaction.ApprovedBy,
            ApproverName = approver != null ? $"{approver.FirstName} {approver.LastName}" : null,
            TransactionDate = transaction.TransactionDate,
            Reference = transaction.Reference,
            CreatedAt = transaction.CreatedAt
        };
    }
}


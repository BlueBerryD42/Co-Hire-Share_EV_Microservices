using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Contracts;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Group.Api.Services;

public class FundService : IFundService
{
    private const decimal LargeWithdrawalThreshold = 1000m; // Configurable threshold
    private readonly GroupDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FundService> _logger;
    private readonly IUserServiceClient _userServiceClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FundService(
        GroupDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<FundService> logger,
        IUserServiceClient userServiceClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userServiceClient = userServiceClient ?? throw new ArgumentNullException(nameof(userServiceClient));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    private string GetAccessToken()
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return string.Empty;
        }
        return authHeader.Substring("Bearer ".Length).Trim();
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

        // Fetch user data via HTTP for member contributions
        var accessToken = GetAccessToken();
        var depositUserIds = allTransactions
            .Where(t => t.Type == FundTransactionType.Deposit && t.Status == FundTransactionStatus.Completed)
            .Select(t => t.InitiatedBy)
            .Distinct()
            .ToList();
        
        var users = await _userServiceClient.GetUsersAsync(depositUserIds, accessToken);
        
        var memberContributions = allTransactions
            .Where(t => t.Type == FundTransactionType.Deposit && t.Status == FundTransactionStatus.Completed)
            .GroupBy(t => t.InitiatedBy)
            .ToDictionary(
                g => users.ContainsKey(g.Key) ? $"{users[g.Key].FirstName} {users[g.Key].LastName}" : "Unknown",
                g => g.Sum(t => t.Amount)
            );

        // Fetch user data for recent transactions
        var transactionUserIds = recentTransactions
            .SelectMany(t => new[] { t.InitiatedBy, t.ApprovedBy })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var transactionUsers = await _userServiceClient.GetUsersAsync(transactionUserIds, accessToken);

        return new FundBalanceDto
        {
            GroupId = fund.GroupId,
            TotalBalance = fund.TotalBalance,
            ReserveBalance = fund.ReserveBalance,
            AvailableBalance = fund.AvailableBalance,
            LastUpdated = fund.LastUpdated,
            RecentTransactions = recentTransactions.Select(t => 
                MapToDto(t, 
                    transactionUsers.GetValueOrDefault(t.InitiatedBy),
                    t.ApprovedBy.HasValue ? transactionUsers.GetValueOrDefault(t.ApprovedBy.Value) : null)).ToList(),
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

            var accessToken = GetAccessToken();
            var initiator = await _userServiceClient.GetUserAsync(userId, accessToken);
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

        var accessToken = GetAccessToken();
        var initiator = await _userServiceClient.GetUserAsync(userId, accessToken);
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

            var accessToken = GetAccessToken();
            var initiator = await _userServiceClient.GetUserAsync(userId, accessToken);
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

            var accessToken = GetAccessToken();
            var initiator = await _userServiceClient.GetUserAsync(userId, accessToken);
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

        // Fetch user data via HTTP
        var accessToken = GetAccessToken();
        var transactionUserIds = transactions
            .SelectMany(t => new[] { t.InitiatedBy, t.ApprovedBy })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var users = await _userServiceClient.GetUsersAsync(transactionUserIds, accessToken);

        return new FundTransactionHistoryDto
        {
            Transactions = transactions.Select(t => 
                MapToDto(t, 
                    users.GetValueOrDefault(t.InitiatedBy),
                    t.ApprovedBy.HasValue ? users.GetValueOrDefault(t.ApprovedBy.Value) : null)).ToList(),
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

        // Fetch user data via HTTP for member contributions
        var accessToken = GetAccessToken();
        var depositUserIds = transactions
            .Where(t => t.Type == FundTransactionType.Deposit)
            .Select(t => t.InitiatedBy)
            .Distinct()
            .ToList();
        var users = await _userServiceClient.GetUsersAsync(depositUserIds, accessToken);

        var memberContributions = transactions
            .Where(t => t.Type == FundTransactionType.Deposit)
            .GroupBy(t => t.InitiatedBy)
            .ToDictionary(
                g => users.ContainsKey(g.Key) ? $"{users[g.Key].FirstName} {users[g.Key].LastName}" : "Unknown",
                g => g.Sum(t => t.Amount)
            );

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

    public async Task<FundTransactionDto> PayExpenseFromFundAsync(Guid groupId, Guid expenseId, decimal amount, string description, Guid initiatedBy)
    {
        // Validate user is member
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == initiatedBy);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than 0");
        }

        var fund = await _context.GroupFunds
            .FirstOrDefaultAsync(f => f.GroupId == groupId);

        if (fund == null)
        {
            throw new InvalidOperationException("Fund not found for this group");
        }

        if (fund.AvailableBalance < amount)
        {
            throw new InvalidOperationException($"Insufficient fund balance. Available: {fund.AvailableBalance}, Required: {amount}");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var balanceBefore = fund.TotalBalance;
            fund.TotalBalance -= amount;
            fund.LastUpdated = DateTime.UtcNow;

            var fundTransaction = new FundTransaction
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                InitiatedBy = initiatedBy,
                Type = FundTransactionType.ExpensePayment,
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceBefore - amount,
                Description = description,
                Status = FundTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow,
                Reference = expenseId.ToString(), // Store expense ID as reference
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.FundTransactions.Add(fundTransaction);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Fetch user data for the transaction
            var accessToken = GetAccessToken();
            var initiator = await _userServiceClient.GetUserAsync(initiatedBy, accessToken);

            _logger.LogInformation("Expense {ExpenseId} paid from fund for group {GroupId}. Amount: {Amount}", 
                expenseId, groupId, amount);

            return MapToDto(fundTransaction, initiator, null);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<FundTransactionDto> CompleteDepositFromPaymentAsync(
        Guid groupId, 
        decimal amount, 
        string description, 
        string paymentReference, 
        Guid initiatedBy, 
        string? reference)
    {
        // Validate user is member (payment already succeeded, but we still check membership)
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == initiatedBy);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than 0");
        }

        // Check if this payment reference was already processed (prevent duplicate deposits)
        var existingTransaction = await _context.FundTransactions
            .FirstOrDefaultAsync(t => t.GroupId == groupId && t.Reference == paymentReference);

        if (existingTransaction != null)
        {
            _logger.LogWarning("Fund deposit with payment reference {PaymentReference} already processed for group {GroupId}", 
                paymentReference, groupId);
            // Return existing transaction instead of throwing error
            var accessToken = GetAccessToken();
            var initiator = await _userServiceClient.GetUserAsync(initiatedBy, accessToken);
            return MapToDto(existingTransaction, initiator, null);
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
            fund.TotalBalance += amount;
            fund.LastUpdated = DateTime.UtcNow;

            var fundTransaction = new FundTransaction
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                InitiatedBy = initiatedBy,
                Type = FundTransactionType.Deposit,
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceBefore + amount,
                Description = description,
                Status = FundTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow,
                Reference = paymentReference, // Store VNPay transaction ID as reference
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.FundTransactions.Add(fundTransaction);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Fetch user data for the transaction
            var accessToken = GetAccessToken();
            var initiator = await _userServiceClient.GetUserAsync(initiatedBy, accessToken);

            _logger.LogInformation("Fund deposit completed from payment {PaymentReference} for group {GroupId}. Amount: {Amount}", 
                paymentReference, groupId, amount);

            return MapToDto(fundTransaction, initiator, null);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static FundTransactionDto MapToDto(FundTransaction transaction, UserInfoDto? initiator, UserInfoDto? approver)
    {
        return new FundTransactionDto
        {
            Id = transaction.Id,
            GroupId = transaction.GroupId,
            InitiatedBy = transaction.InitiatedBy,
            InitiatorName = initiator != null ? $"{initiator.FirstName} {initiator.LastName}" : "Unknown",
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

    public async Task<FundTransactionDto> ApproveWithdrawalAsync(Guid groupId, Guid transactionId, Guid userId)
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
            throw new UnauthorizedAccessException("Only group admins can approve withdrawals");
        }

        var transaction = await _context.FundTransactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.GroupId == groupId);

        if (transaction == null)
        {
            throw new InvalidOperationException("Transaction not found");
        }

        if (transaction.Type != FundTransactionType.Withdrawal)
        {
            throw new InvalidOperationException("Only withdrawal transactions can be approved");
        }

        if (transaction.Status != FundTransactionStatus.Pending)
        {
            throw new InvalidOperationException($"Transaction is not pending. Current status: {transaction.Status}");
        }

        var fund = await _context.GroupFunds
            .FirstOrDefaultAsync(f => f.GroupId == groupId);

        if (fund == null)
        {
            throw new InvalidOperationException("Fund not found for this group");
        }

        if (fund.AvailableBalance < transaction.Amount)
        {
            throw new InvalidOperationException("Insufficient available balance");
        }

        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Update transaction status
            transaction.Status = FundTransactionStatus.Completed;
            transaction.ApprovedBy = userId;
            transaction.UpdatedAt = DateTime.UtcNow;

            // Update fund balance
            fund.TotalBalance -= transaction.Amount;
            fund.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Withdrawal {TransactionId} approved for group {GroupId} by user {UserId}",
                transactionId, groupId, userId);

            // Publish event
            await _publishEndpoint.Publish(new FundWithdrawalEvent
            {
                TransactionId = transaction.Id,
                GroupId = groupId,
                WithdrawnBy = transaction.InitiatedBy,
                Amount = transaction.Amount,
                BalanceAfter = fund.TotalBalance,
                Reason = transaction.Description,
                Status = FundTransactionStatus.Completed,
                WithdrawnAt = transaction.TransactionDate
            });

            var accessToken = GetAccessToken();
            var initiator = await _userServiceClient.GetUserAsync(transaction.InitiatedBy, accessToken);
            var approver = await _userServiceClient.GetUserAsync(userId, accessToken);
            return MapToDto(transaction, initiator, approver);
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    public async Task<FundTransactionDto> RejectWithdrawalAsync(Guid groupId, Guid transactionId, Guid userId, string? reason = null)
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
            throw new UnauthorizedAccessException("Only group admins can reject withdrawals");
        }

        var transaction = await _context.FundTransactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.GroupId == groupId);

        if (transaction == null)
        {
            throw new InvalidOperationException("Transaction not found");
        }

        if (transaction.Type != FundTransactionType.Withdrawal)
        {
            throw new InvalidOperationException("Only withdrawal transactions can be rejected");
        }

        if (transaction.Status != FundTransactionStatus.Pending)
        {
            throw new InvalidOperationException($"Transaction is not pending. Current status: {transaction.Status}");
        }

        // Update transaction status
        transaction.Status = FundTransactionStatus.Rejected;
        transaction.ApprovedBy = userId;
        transaction.UpdatedAt = DateTime.UtcNow;
        
        // Store rejection reason in description if provided
        if (!string.IsNullOrEmpty(reason))
        {
            transaction.Description = $"{transaction.Description} [Rejected: {reason}]";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Withdrawal {TransactionId} rejected for group {GroupId} by user {UserId}. Reason: {Reason}",
            transactionId, groupId, userId, reason ?? "No reason provided");

        // Publish event
        await _publishEndpoint.Publish(new FundWithdrawalEvent
        {
            TransactionId = transaction.Id,
            GroupId = groupId,
            WithdrawnBy = transaction.InitiatedBy,
            Amount = transaction.Amount,
            BalanceAfter = transaction.BalanceBefore, // Balance unchanged on rejection
            Reason = transaction.Description,
            Status = FundTransactionStatus.Rejected,
            WithdrawnAt = transaction.TransactionDate
        });

        var accessToken = GetAccessToken();
        var initiator = await _userServiceClient.GetUserAsync(transaction.InitiatedBy, accessToken);
        var rejector = await _userServiceClient.GetUserAsync(userId, accessToken);
        return MapToDto(transaction, initiator, rejector);
    }
}


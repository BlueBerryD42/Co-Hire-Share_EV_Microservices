using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Group.Api.Contracts;

public interface IFundService
{
    Task<FundBalanceDto> GetFundBalanceAsync(Guid groupId, Guid userId);
    Task<FundTransactionDto> DepositFundAsync(Guid groupId, DepositFundDto depositDto, Guid userId);
    Task<FundTransactionDto> WithdrawFundAsync(Guid groupId, WithdrawFundDto withdrawDto, Guid userId);
    Task<FundTransactionDto> AllocateReserveAsync(Guid groupId, AllocateReserveDto allocateDto, Guid userId);
    Task<FundTransactionDto> ReleaseReserveAsync(Guid groupId, ReleaseReserveDto releaseDto, Guid userId);
    Task<FundTransactionHistoryDto> GetTransactionHistoryAsync(Guid groupId, Guid userId, int page = 1, int pageSize = 20, FundTransactionType? type = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<FundSummaryDto> GetFundSummaryAsync(Guid groupId, Guid userId, string period);
}


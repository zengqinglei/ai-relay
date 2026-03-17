using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 记录成功请求输入 DTO
/// </summary>
public record RecordSuccessInputDto(
    Guid AccountId,
    ProviderPlatform Platform);

using AiRelay.Domain.ProviderAccounts.ValueObjects;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.UsageRecords.Dtos.Query;

/// <summary>
/// 使用记录分页查询输入
/// </summary>
public record UsageRecordPagedInputDto : PagedRequestDto
{
    /// <summary>
    /// API KEY 名称（模糊匹配）
    /// </summary>
    public string? ApiKeyName { get; init; }

    /// <summary>
    /// 请求模型（模糊匹配）
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// 供应商账户名称（模糊匹配）
    /// </summary>
    public string? AccountTokenName { get; init; }

    /// <summary>
    /// 分组 ID
    /// </summary>
    public Guid? ProviderGroupId { get; init; }

    /// <summary>
    /// 平台类型
    /// </summary>
    public ProviderPlatform? Platform { get; init; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; init; }
}

using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.UsageRecords.ValueObjects;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.UsageRecords.Dtos.Query;

/// <summary>
/// 使用记录分页查询输入
/// </summary>
public record UsageRecordPagedInputDto : PagedRequestDto
{
    /// <summary>
    /// 关键字（模糊匹配：API KEY 名、模型、供应商账户、UserAgent、SessionId、RequestUrl、IpAddress）
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// 状态
    /// </summary>
    public UsageStatus? Status { get; init; }

    /// <summary>
    /// 供应商
    /// </summary>
    public Provider? Provider { get; init; }

    /// <summary>
    /// 分组 ID
    /// </summary>
    public Guid? ProviderGroupId { get; init; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; init; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// 认证方式
    /// </summary>
    public AuthMethod? AuthMethod { get; init; }

    /// <summary>
    /// 使用来源
    /// </summary>
    public UsageSource? Source { get; init; }

    /// <summary>
    /// 是否仅显示当前用户数据
    /// </summary>
    public bool? OnlyCurrentUser { get; init; }
}

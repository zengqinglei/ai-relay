using AiRelay.Application.UsageRecords.Dtos.Query;
using Leistd.Ddd.Application.Contracts.Dtos;

namespace AiRelay.Application.UsageRecords.AppServices;

/// <summary>
/// 使用记录应用服务接口
/// </summary>
public interface IUsageRecordAppService
{
    /// <summary>
    /// 查询使用记录列表（分页、排序、筛选）
    /// </summary>
    Task<PagedResultDto<UsageRecordOutputDto>> GetPagedListAsync(UsageRecordPagedInputDto input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取使用记录详情
    /// </summary>
    Task<UsageRecordDetailOutputDto> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
}

using AiRelay.Application.UsageRecords.AppServices;
using AiRelay.Application.UsageRecords.Dtos.Query;
using Leistd.Ddd.Application.Contracts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 使用记录控制器
/// </summary>
[Authorize]
[Route("api/v1/usage-records")]
public class UsageRecordController(IUsageRecordAppService usageRecordAppService) : BaseController
{
    /// <summary>
    /// 查询使用记录列表（分页、排序、筛选）
    /// </summary>
    [HttpGet]
    public async Task<PagedResultDto<UsageRecordOutputDto>> GetPagedListAsync(
        [FromQuery] UsageRecordPagedInputDto input,
        CancellationToken cancellationToken)
    {
        return await usageRecordAppService.GetPagedListAsync(input, cancellationToken);
    }

    /// <summary>
    /// 获取使用记录详情
    /// </summary>
    [HttpGet("{id}/detail")]
    public async Task<UsageRecordDetailOutputDto> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        return await usageRecordAppService.GetDetailAsync(id, cancellationToken);
    }
}

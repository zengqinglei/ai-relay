using System.Linq.Dynamic.Core;
using AiRelay.Application.UsageRecords.Dtos.Query;
using AiRelay.Domain.UsageRecords.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.EntityFrameworkCore;

namespace AiRelay.Application.UsageRecords.AppServices;

/// <summary>
/// 使用记录应用服务实现
/// </summary>
public class UsageRecordAppService(
    IRepository<UsageRecord, Guid> usageRecordRepository,
    IQueryableAsyncExecuter asyncExecuter,
    IObjectMapper objectMapper) : BaseAppService, IUsageRecordAppService
{
    public async Task<UsageRecordDetailOutputDto> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await usageRecordRepository.GetQueryIncludingAsync(x => x.Detail);
        var record = await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id), cancellationToken);

        if (record == null)
        {
            throw new NotFoundException($"Usage record with id {id} not found.");
        }

        // 优先使用 Detail 中的数据，如果 Detail 不存在或字段为空，则回退到主记录中的数据（如果适用）
        // 实际上 DTO 映射会在 Profile 中定义
        return objectMapper.Map<UsageRecord, UsageRecordDetailOutputDto>(record);
    }

    public async Task<PagedResultDto<UsageRecordOutputDto>> GetPagedListAsync(
        UsageRecordPagedInputDto input,
        CancellationToken cancellationToken = default)
    {
        var query = await usageRecordRepository.GetQueryableAsync(cancellationToken);

        // 应用筛选条件
        if (!string.IsNullOrWhiteSpace(input.ApiKeyName))
        {
            query = query.Where(r => r.ApiKeyName != null && r.ApiKeyName.Contains(input.ApiKeyName));
        }

        if (!string.IsNullOrWhiteSpace(input.Model))
        {
            query = query.Where(r =>
                (r.DownModelId != null && r.DownModelId.Contains(input.Model)) ||
                (r.UpModelId != null && r.UpModelId.Contains(input.Model)));
        }

        if (!string.IsNullOrWhiteSpace(input.AccountTokenName))
        {
            query = query.Where(r => r.AccountTokenName != null && r.AccountTokenName.Contains(input.AccountTokenName));
        }

        if (input.ProviderGroupId.HasValue)
        {
            query = query.Where(r => r.ProviderGroupId == input.ProviderGroupId.Value);
        }

        if (input.Platform.HasValue)
        {
            query = query.Where(r => r.Platform == input.Platform.Value);
        }

        if (input.StartTime.HasValue)
        {
            query = query.Where(r => r.CreationTime >= input.StartTime.Value);
        }

        if (input.EndTime.HasValue)
        {
            query = query.Where(r => r.CreationTime <= input.EndTime.Value);
        }

        // 获取总数
        var totalCount = await asyncExecuter.CountAsync(query, cancellationToken);

        // 使用动态排序，null 值按 0 处理
        var sorting = input.Sorting ?? $"{nameof(UsageRecord.CreationTime)} desc";
        // 对可能为 null 的数值字段做 null-safe 处理，避免 null 排到最前
        sorting = System.Text.RegularExpressions.Regex.Replace(
            sorting,
            @"\b(finalCost|inputTokens|outputTokens|durationMs)\b",
            m => $"({m.Value} ?? 0)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var sortedQuery = query.OrderBy(sorting);

        // 应用分页
        var records = await asyncExecuter.ToListAsync(
            sortedQuery.Skip(input.Offset).Take(input.Limit),
            cancellationToken);

        var items = objectMapper.Map<List<UsageRecord>, List<UsageRecordOutputDto>>(records);

        return new PagedResultDto<UsageRecordOutputDto>(totalCount, items);
    }
}

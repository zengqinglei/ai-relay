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
        var baseQuery = await usageRecordRepository.GetQueryIncludingAsync(cancellationToken, x => x.Detail);
        var query = baseQuery.Include(x => x.Attempts).ThenInclude(a => a.Detail);
        var record = await asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id), cancellationToken);

        if (record == null)
        {
            throw new NotFoundException($"Usage record with id {id} not found.");
        }

        return objectMapper.Map<UsageRecord, UsageRecordDetailOutputDto>(record);
    }

    public async Task<PagedResultDto<UsageRecordOutputDto>> GetPagedListAsync(
        UsageRecordPagedInputDto input,
        CancellationToken cancellationToken = default)
    {
        var query = await usageRecordRepository.GetQueryIncludingAsync(cancellationToken, p => p.Attempts);

        // 应用筛选条件
        if (!string.IsNullOrWhiteSpace(input.ApiKeyName))
        {
            query = query.Where(r => r.ApiKeyName != null && r.ApiKeyName.Contains(input.ApiKeyName));
        }

        if (!string.IsNullOrWhiteSpace(input.Model))
        {
            query = query.Where(r =>
                (r.DownModelId != null && r.DownModelId.Contains(input.Model)));
        }

        if (!string.IsNullOrWhiteSpace(input.AccountTokenName))
        {
            query = query.Where(r => r.Attempts.Any(a => a.AccountTokenName.Contains(input.AccountTokenName)));
        }

        if (input.ProviderGroupId.HasValue)
        {
            query = query.Where(r => r.Attempts.Any(a => a.ProviderGroupId == input.ProviderGroupId.Value));
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
        sorting = System.Text.RegularExpressions.Regex.Replace(
            sorting,
            @"\b(finalCost|inputTokens|outputTokens|durationMs)\b",
            m => $"({m.Value} ?? 0)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var sortedQuery = query.OrderBy(sorting);

        // 分页 + LEFT JOIN 最新一次 Attempt
        var pagedRecords = await asyncExecuter.ToListAsync(
            sortedQuery
                .Skip(input.Offset)
                .Take(input.Limit)
                .Select(r => new
                {
                    Record = r,
                    LatestAttempt = r.Attempts
                        .OrderByDescending(a => a.AttemptNumber)
                        .FirstOrDefault()
                }),
            cancellationToken);

        var items = pagedRecords.Select(x =>
        {
            var dto = objectMapper.Map<UsageRecord, UsageRecordOutputDto>(x.Record);
            dto.ProviderGroupName = x.LatestAttempt?.ProviderGroupName;
            dto.AccountTokenName = x.LatestAttempt?.AccountTokenName;
            dto.UpModelId = x.LatestAttempt?.UpModelId;
            dto.UpStatusCode = x.LatestAttempt?.UpStatusCode;
            return dto;
        }).ToList();

        return new PagedResultDto<UsageRecordOutputDto>(totalCount, items);
    }
}

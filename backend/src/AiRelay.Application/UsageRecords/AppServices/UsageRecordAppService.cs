using System.Linq.Dynamic.Core;
using AiRelay.Application.UsageRecords.Dtos.Query;
using AiRelay.Domain.Users.Entities;
using AiRelay.Domain.Users.Specifications;
using AiRelay.Domain.UsageRecords.Entities;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Leistd.Security.Users;
using Microsoft.EntityFrameworkCore;

namespace AiRelay.Application.UsageRecords.AppServices;

/// <summary>
/// 使用记录应用服务实现
/// </summary>
public class UsageRecordAppService(
    IRepository<UsageRecord, Guid> usageRecordRepository,
    IRepository<UsageRecordAttempt, Guid> usageRecordAttemptRepository,
    IRepository<User, Guid> userRepository,
    IQueryableAsyncExecuter asyncExecuter,
    ICurrentUser currentUser,
    IObjectMapper objectMapper) : BaseAppService, IUsageRecordAppService
{
    public async Task<UsageRecordDetailOutputDto> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IQueryable<UsageRecord> query = await usageRecordRepository.GetQueryIncludingAsync(cancellationToken, x => x.Detail);

        var scopedUserId = UserScopeSpecifications.ResolveScopedUserId(currentUser);
        if (scopedUserId.HasValue)
        {
            query = query.Where(x => x.UserId == scopedUserId.Value);
        }

        var record = await asyncExecuter.SingleOrDefaultAsync(query.Where(x => x.Id == id), cancellationToken);

        if (record == null)
        {
            throw new NotFoundException($"Usage record with id {id} not found.");
        }

        var attemptQuery = await usageRecordAttemptRepository.GetQueryIncludingAsync(cancellationToken, x => x.Detail);
        var attempts = await asyncExecuter.ToListAsync(
            attemptQuery
                .Where(x => x.UsageRecordId == id)
                .OrderBy(x => x.AttemptNumber),
            cancellationToken);

        return new UsageRecordDetailOutputDto
        {
            UsageRecordId = record.Id,
            Source = record.Source,
            ApiKeyName = record.ApiKeyName,
            DownRequestUrl = record.DownRequestUrl,
            DownRequestHeaders = record.Detail?.DownRequestHeaders,
            DownRequestBody = record.Detail?.DownRequestBody,
            DownResponseBody = record.Detail?.DownResponseBody,
            Attempts = objectMapper.Map<List<UsageRecordAttempt>, List<UsageRecordAttemptOutputDto>>(attempts)
        };
    }

    public async Task<PagedResultDto<UsageRecordOutputDto>> GetPagedListAsync(
        UsageRecordPagedInputDto input,
        CancellationToken cancellationToken = default)
    {
        IQueryable<UsageRecord> query = await usageRecordRepository.GetQueryIncludingAsync(cancellationToken, p => p.Attempts);
        var scopedUserId = UserScopeSpecifications.ResolveScopedUserId(currentUser, input.OnlyCurrentUser);
        if (scopedUserId.HasValue)
        {
            query = query.Where(r => r.UserId == scopedUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            var matchedUserIds = await userRepository
                .GetListAsync(u => u.Username.Contains(keyword), cancellationToken);
            var userIds = matchedUserIds.Select(u => u.Id).ToList();

            query = query.Where(r =>
                (r.ApiKeyName != null && r.ApiKeyName.Contains(keyword)) ||
                (r.DownModelId != null && r.DownModelId.Contains(keyword)) ||
                (r.SessionId != null && r.SessionId.Contains(keyword)) ||
                (r.DownRequestUrl != null && r.DownRequestUrl.Contains(keyword)) ||
                (r.DownUserAgent != null && r.DownUserAgent.Contains(keyword)) ||
                (r.DownClientIp != null && r.DownClientIp.Contains(keyword)) ||
                userIds.Contains(r.UserId) ||
                r.Attempts.Any(a =>
                    (a.AccountTokenName != null && a.AccountTokenName.Contains(keyword)) ||
                    (a.UpModelId != null && a.UpModelId.Contains(keyword))
                )
            );
        }

        if (input.Status.HasValue)
        {
            query = query.Where(r => r.Status == input.Status.Value);
        }

        if (input.Source.HasValue)
        {
            query = query.Where(r => r.Source == input.Source.Value);
        }

        if (input.Provider.HasValue)
        {
            query = query.Where(r => r.Attempts.Any(a => a.Provider == input.Provider.Value));
        }

        if (input.ProviderGroupId.HasValue)
        {
            query = query.Where(r => r.Attempts.Any(a => a.ProviderGroupId == input.ProviderGroupId.Value));
        }

        if (input.AuthMethod.HasValue)
        {
            query = query.Where(r => r.Attempts.Any(a => a.AuthMethod == input.AuthMethod.Value));
        }

        if (input.StartTime.HasValue)
        {
            query = query.Where(r => r.CreationTime >= input.StartTime.Value);
        }

        if (input.EndTime.HasValue)
        {
            query = query.Where(r => r.CreationTime <= input.EndTime.Value);
        }

        var totalCount = await asyncExecuter.CountAsync(query, cancellationToken);

        var sorting = input.Sorting ?? $"{nameof(UsageRecord.CreationTime)} desc";
        sorting = System.Text.RegularExpressions.Regex.Replace(
            sorting,
            @"\b(finalCost|inputTokens|outputTokens|durationMs)\b",
            m => $"({m.Value} ?? 0)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var sortedQuery = query.OrderBy(sorting);

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
            dto.Provider = x.LatestAttempt?.Provider;
            dto.AuthMethod = x.LatestAttempt?.AuthMethod;
            return dto;
        }).ToList();

        await FillOwnerInfoAsync(items, cancellationToken);
        return new PagedResultDto<UsageRecordOutputDto>(totalCount, items);
    }

    private async Task FillOwnerInfoAsync(List<UsageRecordOutputDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var userIds = items.Select(x => x.UserId).Distinct().ToList();
        var users = await userRepository.GetListAsync(x => userIds.Contains(x.Id), cancellationToken);
        var userLookup = users.ToDictionary(x => x.Id);

        foreach (var item in items)
        {
            if (!userLookup.TryGetValue(item.UserId, out var user))
            {
                continue;
            }

            item.Username = user.Username;
            item.Email = user.Email;
        }
    }
}

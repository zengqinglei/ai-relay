using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.Utilities;
using Xunit;

namespace AiRelay.Tests.UsageRecords;

/// <summary>
/// 今日统计字段的跨日守卫：StatsDate 落在旧锚点（隔日无流量）时，
/// 读取 UsageToday/CostToday/TokensToday/SuccessToday 必须返回 0，
/// 而累计字段（*Total）不受影响。
/// </summary>
public class TodayStatsGuardTests
{
    private static DateTime TodayAnchor => LocalDayAnchor.GetTodayUtcAnchor();
    private static DateTime YesterdayAnchor => TodayAnchor.AddDays(-1);

    private static AccountToken CreateAccount() =>
        new(Provider.Claude, AuthMethod.ApiKey, "test-account", maxConcurrency: 1);

    private static ApiKey CreateApiKey() =>
        new(Guid.NewGuid(), "test-key", null, "encrypted", "hash");

    [Fact]
    public void AccountToken_TodayStats_AreZero_WhenStatsDateIsStale()
    {
        var account = CreateAccount();
        account.AccumulateCallStats(isSuccess: true, YesterdayAnchor);
        account.AccumulateCostStats(tokens: 100, cost: 1.5m, YesterdayAnchor);

        Assert.Equal(0, account.UsageToday);
        Assert.Equal(0, account.SuccessToday);
        Assert.Equal(0, account.TokensToday);
        Assert.Equal(0m, account.CostToday);
        // 累计字段不受跨日守卫影响
        Assert.Equal(1, account.UsageTotal);
        Assert.Equal(1, account.SuccessTotal);
        Assert.Equal(100, account.TokensTotal);
        Assert.Equal(1.5m, account.CostTotal);
    }

    [Fact]
    public void AccountToken_TodayStats_Visible_WhenStatsDateIsCurrentAnchor()
    {
        var account = CreateAccount();
        account.AccumulateCallStats(isSuccess: true, TodayAnchor);
        account.AccumulateCostStats(tokens: 100, cost: 1.5m, TodayAnchor);

        Assert.Equal(1, account.UsageToday);
        Assert.Equal(1, account.SuccessToday);
        Assert.Equal(100, account.TokensToday);
        Assert.Equal(1.5m, account.CostToday);
    }

    [Fact]
    public void ApiKey_TodayStats_AreZero_WhenStatsDateIsStale()
    {
        var apiKey = CreateApiKey();
        apiKey.AccumulateStats(tokens: 100, cost: 1.5m, isSuccess: true, YesterdayAnchor);

        Assert.Equal(0, apiKey.UsageToday);
        Assert.Equal(0, apiKey.SuccessToday);
        Assert.Equal(0, apiKey.TokensToday);
        Assert.Equal(0m, apiKey.CostToday);
        Assert.Equal(1, apiKey.UsageTotal);
        Assert.Equal(100, apiKey.TokensTotal);
    }

    [Fact]
    public void ApiKey_TodayStats_Visible_WhenStatsDateIsCurrentAnchor()
    {
        var apiKey = CreateApiKey();
        apiKey.AccumulateStats(tokens: 100, cost: 1.5m, isSuccess: true, TodayAnchor);

        Assert.Equal(1, apiKey.UsageToday);
        Assert.Equal(1, apiKey.SuccessToday);
        Assert.Equal(100, apiKey.TokensToday);
        Assert.Equal(1.5m, apiKey.CostToday);
    }
}

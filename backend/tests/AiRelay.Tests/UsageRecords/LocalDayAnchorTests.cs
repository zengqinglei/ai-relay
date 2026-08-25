using AiRelay.Domain.Shared.Utilities;
using Xunit;

namespace AiRelay.Tests.UsageRecords;

/// <summary>
/// 本地自然日锚点：把"当前本地日期的 00:00"换算为 UTC 时间点，
/// 作为今日统计的跨日判定基准。
/// </summary>
public class LocalDayAnchorTests
{
    [Fact]
    public void GetTodayUtcAnchor_ConvertsLocalMidnightToUtc()
    {
        // 北京时间 2026-05-28 04:00 => 本地日期 2026-05-28，其零点对应 UTC 2026-05-27 16:00
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        var nowUtc = new DateTime(2026, 5, 27, 20, 0, 0, DateTimeKind.Utc);

        var anchor = LocalDayAnchor.GetTodayUtcAnchor(tz, nowUtc);

        Assert.Equal(new DateTime(2026, 5, 27, 16, 0, 0, DateTimeKind.Utc), anchor);
    }

    [Fact]
    public void GetTodayUtcAnchor_UtcTimeZone_ReturnsUtcDate()
    {
        var nowUtc = new DateTime(2026, 5, 27, 20, 0, 0, DateTimeKind.Utc);

        var anchor = LocalDayAnchor.GetTodayUtcAnchor(TimeZoneInfo.Utc, nowUtc);

        Assert.Equal(nowUtc.Date, anchor);
    }
}

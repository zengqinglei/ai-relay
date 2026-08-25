namespace AiRelay.Domain.Shared.Utilities;

/// <summary>
/// 本地自然日锚点 — 今日统计的统一跨日判定基准。
/// 锚点定义为"当前系统本地日期的 00:00 对应的 UTC 时间点"，
/// 例如北京时间 2026-05-28 00:00:00 对应 UTC 2026-05-27 16:00:00。
/// 写入方（累计统计）与读取方（getter 守卫、聚合查询）必须使用同一锚点计算。
/// </summary>
public static class LocalDayAnchor
{
    /// <summary>
    /// 获取当前系统本地日期对应的 UTC 零点锚点。
    /// </summary>
    public static DateTime GetTodayUtcAnchor() =>
        GetTodayUtcAnchor(TimeZoneInfo.Local, DateTime.UtcNow);

    /// <summary>
    /// 获取指定时区、指定 UTC 时刻所在本地日期的 UTC 零点锚点。
    /// </summary>
    public static DateTime GetTodayUtcAnchor(TimeZoneInfo timeZone, DateTime nowUtc)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        return TimeZoneInfo.ConvertTimeToUtc(nowLocal.Date, timeZone);
    }
}

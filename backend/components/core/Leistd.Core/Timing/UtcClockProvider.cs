namespace Leistd.Timing;

/// <summary>
/// UTC 时钟提供器（默认实现）
/// </summary>
/// <remarks>
/// 始终返回 UTC 时间，这是推荐的做法：
/// 1. 数据库存储使用 UTC 避免时区混乱
/// 2. 前端展示时再根据用户时区转换
/// 3. 避免夏令时问题
/// </remarks>
public class UtcClockProvider : IClock
{
    /// <summary>
    /// 获取当前 UTC 时间
    /// </summary>
    public DateTime Now => DateTime.UtcNow;

    /// <summary>
    /// 时间类型：UTC
    /// </summary>
    public DateTimeKind Kind => DateTimeKind.Utc;

    /// <summary>
    /// 标准化为 UTC 时间
    /// </summary>
    public DateTime Normalize(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            // 假定未指定类型的时间为 UTC
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }

        if (dateTime.Kind == DateTimeKind.Local)
        {
            // 本地时间转换为 UTC
            return dateTime.ToUniversalTime();
        }

        // 已经是 UTC，直接返回
        return dateTime;
    }
}

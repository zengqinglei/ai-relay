namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Parsing;

/// <summary>
/// OpenAI Duration 解析器
/// 负责解析 OpenAI 返回的时间格式字符串（如 20ms, 6s, 1m, 1h）
/// </summary>
public static class OpenAiDurationParser
{
    /// <summary>
    /// 解析 OpenAI 时间格式字符串
    /// 支持格式: 20ms, 6s, 1m, 1h
    /// </summary>
    public static TimeSpan? ParseDurationString(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return null;

        duration = duration.Trim().ToLowerInvariant();
        try
        {
            if (duration.EndsWith("ms"))
            {
                if (double.TryParse(duration.Replace("ms", ""), out var ms))
                    return TimeSpan.FromMilliseconds(ms);
            }
            else if (duration.EndsWith("s"))
            {
                if (double.TryParse(duration.Replace("s", ""), out var s))
                    return TimeSpan.FromSeconds(s);
            }
            else if (duration.EndsWith("m"))
            {
                if (double.TryParse(duration.Replace("m", ""), out var m))
                    return TimeSpan.FromMinutes(m);
            }
            else if (duration.EndsWith("h"))
            {
                if (double.TryParse(duration.Replace("h", ""), out var h))
                    return TimeSpan.FromHours(h);
            }
        }
        catch { }

        return null;
    }
}

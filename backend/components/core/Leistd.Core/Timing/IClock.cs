namespace Leistd.Timing;

/// <summary>
/// 时钟抽象接口，用于获取当前时间
/// </summary>
/// <remarks>
/// 提供时间抽象的主要目的：
/// 1. 可测试性：单元测试中可以 mock 时间
/// 2. 时区策略：支持不同的时区处理策略
/// 3. 统一管理：所有时间获取通过此接口，便于统一调整
/// </remarks>
public interface IClock
{
    /// <summary>
    /// 获取当前时间
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// 获取时间类型（UTC、Local、Unspecified）
    /// </summary>
    DateTimeKind Kind { get; }

    /// <summary>
    /// 标准化时间（确保时间类型一致）
    /// </summary>
    /// <param name="dateTime">要标准化的时间</param>
    /// <returns>标准化后的时间</returns>
    DateTime Normalize(DateTime dateTime);
}

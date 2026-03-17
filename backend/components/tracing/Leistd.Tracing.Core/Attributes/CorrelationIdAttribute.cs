namespace Leistd.Tracing.Core.Attributes;

/// <summary>
/// 自动开启 TraceId 作用域的注解
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true)]
public class CorrelationIdAttribute : Attribute
{
}

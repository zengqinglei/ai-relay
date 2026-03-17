using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Api.Middleware.SmartProxy.Contexts;

/// <summary>
/// 请求转换上下文（包含业务信息）
/// </summary>
public class TransformContext
{
    // 业务标识
    public Guid RequestId { get; init; }
    public ProviderPlatform Platform { get; init; }
    public Guid ApiKeyId { get; init; }
    public string ApiKeyName { get; init; } = string.Empty;
    public Guid AccountTokenId { get; init; }
    public string AccountTokenName { get; init; } = string.Empty;
    public Guid? ProviderGroupId { get; init; }
    public string? ProviderGroupName { get; init; }

    // 技术上下文（引用）
    public required DownRequestContext DownRequest { get; init; }
    public UpRequestContext? UpRequest { get; set; }
}

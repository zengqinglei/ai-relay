using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;

/// <summary>
/// Claude OAuth 专属：注入 metadata.user_id（基于账号指纹）
/// 原 Middleware.InjectMetadataUserIdAsync 迁移至此，操作 up.BodyJson（CloneBodyJson 后的副本）
/// </summary>
public class ClaudeMetadataInjectProcessor(
    ChatModelConnectionOptions options,
    AccountFingerprintDomainService fingerprintDomainService) : IRequestProcessor
{

    public async Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (up.BodyJson == null) return;

        // 仅在 metadata.user_id 为空时注入
        if (up.BodyJson.TryGetPropertyValue("metadata", out var metadataNode) &&
            metadataNode is JsonObject metadataObj &&
            metadataObj.TryGetPropertyValue("user_id", out var userIdNode) &&
            userIdNode is JsonValue userIdVal &&
            userIdVal.TryGetValue<string>(out var existingUserId) &&
            !string.IsNullOrWhiteSpace(existingUserId))
        {
            return;
        }

        var accountTokenId = Guid.Parse(
            options.ExtraProperties.GetValueOrDefault("account_token_id", Guid.Empty.ToString())!);

        var fingerprint = await fingerprintDomainService.GetOrCreateFingerprintAsync(
            accountTokenId, down.Headers, ct);

        options.ExtraProperties.TryGetValue("account_uuid", out var accountUuid);

        bool enableMasking = options.ExtraProperties.TryGetValue("session_id_masking_enabled",
            out var maskingValue) && bool.TryParse(maskingValue, out var enabled) && enabled;

        var sessionId = await fingerprintDomainService.GenerateSessionUuidAsync(
            accountTokenId, down.SessionHash, enableMasking, ct);

        var userId = !string.IsNullOrWhiteSpace(accountUuid)
            ? $"user_{fingerprint.ClientId}_account_{accountUuid.Trim()}_session_{sessionId}"
            : $"user_{fingerprint.ClientId}_account__session_{sessionId}";

        if (!up.BodyJson.ContainsKey("metadata"))
            up.BodyJson["metadata"] = new JsonObject();

        if (up.BodyJson["metadata"] is JsonObject metadata)
            metadata["user_id"] = userId;
    }
}

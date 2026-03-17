using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;

/// <summary>
/// 模型提供者服务
/// </summary>
/// <remarks>
/// 职责：
/// 1. 提供 Antigravity 模型名称映射
/// 2. 提供各平台可用模型列表（用于前端展示）
/// </remarks>
public interface IModelProvider
{
    /// <summary>
    /// 获取 Antigravity 映射后的模型名称
    /// </summary>
    /// <param name="requestedModel">客户端请求的模型名称</param>
    /// <returns>Antigravity 实际调用的模型名称</returns>
    /// <remarks>
    /// 仅适用于 ProviderPlatform.ANTIGRAVITY 平台
    /// </remarks>
    string GetAntigravityMappedModel(string requestedModel);

    /// <summary>
    /// 获取 OpenAI Codex 映射后的模型名称
    /// </summary>
    /// <param name="requestedModel">客户端请求的模型名称</param>
    /// <returns>OpenAI Codex 实际调用的模型名称</returns>
    /// <remarks>
    /// 适用于 ProviderPlatform.OPENAI_OAUTH 和 OPENAI_APIKEY 平台
    /// </remarks>
    string GetOpenAIMappedModel(string requestedModel);

    /// <summary>
    /// 获取 Claude 映射后的模型名称
    /// </summary>
    /// <param name="requestedModel">客户端请求的模型名称</param>
    /// <returns>Claude 实际调用的模型名称</returns>
    /// <remarks>
    /// 适用于 ProviderPlatform.CLAUDE_OAUTH 和 CLAUDE_APIKEY 平台
    /// </remarks>
    string GetClaudeMappedModel(string requestedModel);

    /// <summary>
    /// 获取指定平台的可用模型列表
    /// </summary>
    /// <param name="platform">平台类型</param>
    /// <returns>模型选项列表</returns>
    IReadOnlyList<ModelOption> GetAvailableModels(ProviderPlatform platform);
}

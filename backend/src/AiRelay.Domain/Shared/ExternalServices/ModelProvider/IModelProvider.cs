using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;

namespace AiRelay.Domain.Shared.ExternalServices.ModelProvider;

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
    /// 仅适用于 Provider.Antigravity 提供商
    /// </remarks>
    string GetAntigravityMappedModel(string requestedModel);

    /// <summary>
    /// 获取 OpenAI Codex 映射后的模型名称
    /// </summary>
    /// <param name="requestedModel">客户端请求的模型名称</param>
    /// <returns>OpenAI Codex 实际调用的模型名称</returns>
    /// <remarks>
    /// 适用于 Provider.OpenAI 提供商
    /// </remarks>
    string GetOpenAIMappedModel(string requestedModel);

    /// <summary>
    /// 获取 Claude 映射后的模型名称
    /// </summary>
    /// <param name="requestedModel">客户端请求的模型名称</param>
    /// <returns>Claude 实际调用的模型名称</returns>
    /// <remarks>
    /// 适用于 Provider.Claude 提供商
    /// </remarks>
    string GetClaudeMappedModel(string requestedModel);

    /// <summary>
    /// 获取指定平台的可用模型列表
    /// </summary>
    /// <param name="provider">提供商</param>
    /// <returns>模型选项列表</returns>
    IReadOnlyList<ModelOption> GetAvailableModels(Provider provider);
}

using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;

namespace AiRelay.Domain.Shared.ExternalServices.ModelProvider;

/// <summary>
/// 模型提供者服务
/// </summary>
/// <remarks>
/// 职责：
/// 1. 提供各平台模型名称映射（Antigravity / Claude / OpenAI / OpenAI Compatible）
/// 2. 提供各平台可用模型列表（用于前端展示）
/// </remarks>
public interface IModelProvider
{
    /// <summary>
    /// 获取指定平台映射后的模型名称
    /// </summary>
    /// <param name="provider">提供商（用于选择对应的映射表）</param>
    /// <param name="requestedModel">客户端请求的模型名称</param>
    /// <returns>该平台实际调用的模型名称；若无映射则透传原始值</returns>
    string GetMappedModel(Provider provider, string requestedModel);

    /// <summary>
    /// 获取指定平台的可用模型列表
    /// </summary>
    /// <param name="provider">提供商</param>
    /// <returns>模型选项列表</returns>
    IReadOnlyList<ModelOption> GetAvailableModels(Provider provider);
}

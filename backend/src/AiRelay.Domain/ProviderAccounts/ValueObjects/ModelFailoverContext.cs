namespace AiRelay.Domain.ProviderAccounts.ValueObjects;

/// <summary>
/// 模型级 Failover 上下文 — 持有候选模型优先级列表，当当前模型的账号池耗尽时切换到下一个。
/// 仅在 "auto" 模型场景下构建；普通请求为 null，不影响现有逻辑。
/// </summary>
public sealed class ModelFailoverContext
{
    /// <summary>
    /// 按优先级排列的候选模型 ID 列表（来自 DefaultProviderModelsOptions.Models）
    /// </summary>
    public required IReadOnlyList<string> CandidateModels { get; init; }

    /// <summary>
    /// 当前正在使用的模型在 CandidateModels 中的索引
    /// </summary>
    public int CurrentModelIndex { get; set; }

    /// <summary>
    /// 当前模型 ID
    /// </summary>
    public string CurrentModel => CandidateModels[CurrentModelIndex];

    /// <summary>
    /// 是否还有下一个候选模型可切换
    /// </summary>
    public bool HasNextModel => CurrentModelIndex < CandidateModels.Count - 1;
}

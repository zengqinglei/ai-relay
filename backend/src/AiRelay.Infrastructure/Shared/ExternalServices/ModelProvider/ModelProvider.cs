using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider;
using AiRelay.Domain.Shared.ExternalServices.ModelProvider.Dto;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelProvider;

/// <summary>
/// 模型提供者服务实现（支持通配符匹配）
/// </summary>
/// <remarks>
/// 三层路由引擎（参考 Antigravity-Manager/model_mapping.rs）：
/// 1. 精确匹配：检查映射表中的精确映射（最高优先级）
/// 2. 通配符匹配：支持 * 通配符，按特异性评分选择最佳匹配
/// 3. 系统默认映射：透传策略 + 智能降级 + 默认降级
/// </remarks>
public sealed class ModelProvider(ILogger<ModelProvider> logger) : IModelProvider
{
    /// <summary>
    /// 用户自定义模型映射表（支持通配符）
    /// </summary>
    private static readonly Dictionary<string, string> CustomModelMappings = new();

    /// <summary>
    /// Claude 模型映射表
    /// </summary>
    private static readonly Dictionary<string, string> ClaudeModelMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Claude 4.5 系列
        ["claude-opus-4-5"] = "claude-opus-4-5-20251101",
        ["claude-haiku-4-5"] = "claude-haiku-4-5-20251001",
        ["claude-sonnet-4-5"] = "claude-sonnet-4-5-20250929"
    };

    /// <summary>
    /// OpenAI Codex 模型映射表
    /// </summary>
    private static readonly Dictionary<string, string> OpenAICodexModelMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // GPT-5.4 系列
        ["gpt-5.4"] = "gpt-5.4",
        ["gpt-5.4-none"] = "gpt-5.4",
        ["gpt-5.4-low"] = "gpt-5.4",
        ["gpt-5.4-medium"] = "gpt-5.4",
        ["gpt-5.4-high"] = "gpt-5.4",
        ["gpt-5.4-xhigh"] = "gpt-5.4",
        ["gpt-5.4-chat-latest"] = "gpt-5.4",

        // GPT-5.3 系列
        ["gpt-5.3"] = "gpt-5.3-codex",
        ["gpt-5.3-none"] = "gpt-5.3-codex",
        ["gpt-5.3-low"] = "gpt-5.3-codex",
        ["gpt-5.3-medium"] = "gpt-5.3-codex",
        ["gpt-5.3-high"] = "gpt-5.3-codex",
        ["gpt-5.3-xhigh"] = "gpt-5.3-codex",
        ["gpt-5.3-codex"] = "gpt-5.3-codex",
        ["gpt-5.3-codex-spark"] = "gpt-5.3-codex",
        ["gpt-5.3-codex-spark-low"] = "gpt-5.3-codex",
        ["gpt-5.3-codex-spark-medium"] = "gpt-5.3-codex",
        ["gpt-5.3-codex-spark-high"] = "gpt-5.3-codex",
        ["gpt-5.3-codex-spark-xhigh"] = "gpt-5.3-codex",
        ["gpt-5.3-codex-low"] = "gpt-5.3-codex",
        ["gpt-5.3-codex-medium"] = "gpt-5.3-codex",
        ["gpt-5.3-codex-high"] = "gpt-5.3-codex",
        ["gpt-5.3-codex-xhigh"] = "gpt-5.3-codex",

        // GPT-5.2 系列
        ["gpt-5.2"] = "gpt-5.2",
        ["gpt-5.2-none"] = "gpt-5.2",
        ["gpt-5.2-low"] = "gpt-5.2",
        ["gpt-5.2-medium"] = "gpt-5.2",
        ["gpt-5.2-high"] = "gpt-5.2",
        ["gpt-5.2-xhigh"] = "gpt-5.2",
        ["gpt-5.2-codex"] = "gpt-5.2-codex",
        ["gpt-5.2-codex-low"] = "gpt-5.2-codex",
        ["gpt-5.2-codex-medium"] = "gpt-5.2-codex",
        ["gpt-5.2-codex-high"] = "gpt-5.2-codex",
        ["gpt-5.2-codex-xhigh"] = "gpt-5.2-codex",

        // GPT-5.1 系列
        ["gpt-5.1-codex"] = "gpt-5.1-codex",
        ["gpt-5.1-codex-low"] = "gpt-5.1-codex",
        ["gpt-5.1-codex-medium"] = "gpt-5.1-codex",
        ["gpt-5.1-codex-high"] = "gpt-5.1-codex",
        ["gpt-5.1-codex-max"] = "gpt-5.1-codex-max",
        ["gpt-5.1-codex-max-low"] = "gpt-5.1-codex-max",
        ["gpt-5.1-codex-max-medium"] = "gpt-5.1-codex-max",
        ["gpt-5.1-codex-max-high"] = "gpt-5.1-codex-max",
        ["gpt-5.1-codex-max-xhigh"] = "gpt-5.1-codex-max",
        ["gpt-5.1-codex-mini"] = "gpt-5.1-codex-mini",
        ["gpt-5.1-codex-mini-medium"] = "gpt-5.1-codex-mini",
        ["gpt-5.1-codex-mini-high"] = "gpt-5.1-codex-mini",
        ["gpt-5.1"] = "gpt-5.1",
        ["gpt-5.1-none"] = "gpt-5.1",
        ["gpt-5.1-low"] = "gpt-5.1",
        ["gpt-5.1-medium"] = "gpt-5.1",
        ["gpt-5.1-high"] = "gpt-5.1",
        ["gpt-5.1-chat-latest"] = "gpt-5.1",

        // GPT-5 别名
        ["gpt-5-codex"] = "gpt-5.1-codex",
        ["codex-mini-latest"] = "gpt-5.1-codex-mini",
        ["gpt-5-codex-mini"] = "gpt-5.1-codex-mini",
        ["gpt-5-codex-mini-medium"] = "gpt-5.1-codex-mini",
        ["gpt-5-codex-mini-high"] = "gpt-5.1-codex-mini",
        ["gpt-5"] = "gpt-5.1",
        ["gpt-5-mini"] = "gpt-5.1",
        ["gpt-5-nano"] = "gpt-5.1",

        // GPT-4 系列
        ["gpt-4o"] = "gpt-4o-2024-05-13",
        ["gpt-4o-2024-05-13"] = "gpt-4o-2024-05-13"
    };

    /// <summary>
    /// Antigravity 模型精确映射表（对应 Rust 的 CLAUDE_TO_GEMINI HashMap）
    /// </summary>
    private static readonly Dictionary<string, string> AntigravityModelMappings = new()
    {
        // Claude 4.6 系列
        ["claude-opus-4-6-thinking"] = "claude-opus-4-6-thinking",
        ["claude-opus-4-6"] = "claude-opus-4-6-thinking",
        ["claude-sonnet-4-6"] = "claude-sonnet-4-6",

        // Claude 4.5 系列
        ["claude-opus-4-5-thinking"] = "claude-opus-4-6-thinking", // 迁移到 4.6
        ["claude-opus-4-5-20251101"] = "claude-opus-4-6-thinking", // 迁移旧模型
        ["claude-sonnet-4-5"] = "claude-sonnet-4-5",
        ["claude-sonnet-4-5-thinking"] = "claude-sonnet-4-5-thinking",
        ["claude-sonnet-4-5-20250929"] = "claude-sonnet-4-5",
        ["claude-haiku-4-5"] = "claude-sonnet-4-5",
        ["claude-haiku-4-5-20251001"] = "claude-sonnet-4-5",

        // Gemini 3.1 系列
        ["gemini-3.1-pro-high"] = "gemini-3.1-pro-high",
        ["gemini-3.1-pro-low"] = "gemini-3.1-pro-low",
        ["gemini-3.1-pro-preview"] = "gemini-3.1-pro-high",
        ["gemini-3.1-flash-image"] = "gemini-3.1-flash-image",
        ["gemini-3.1-flash-image-preview"] = "gemini-3.1-flash-image",

        // Gemini 3 系列
        ["gemini-3-flash"] = "gemini-3-flash",
        ["gemini-3-pro-high"] = "gemini-3-pro-high",
        ["gemini-3-pro-low"] = "gemini-3-pro-low",
        ["gemini-3-pro"] = "gemini-3-pro-high",
        ["gemini-3-flash-preview"] = "gemini-3-flash",
        ["gemini-3-pro-preview"] = "gemini-3-pro-high",
        ["gemini-3-pro-image"] = "gemini-3.1-flash-image",
        ["gemini-3-pro-image-preview"] = "gemini-3.1-flash-image",

        // Gemini 2.5 系列
        ["gemini-2.5-pro"] = "gemini-2.5-pro",
        ["gemini-2.5-flash"] = "gemini-2.5-flash",
        ["gemini-2.5-flash-thinking"] = "gemini-2.5-flash-thinking",
        ["gemini-2.5-flash-lite"] = "gemini-2.5-flash-lite",

        // 其他官方模型
        ["gpt-oss-120b-medium"] = "gpt-oss-120b-medium",
        ["tab_flash_lite_preview"] = "tab_flash_lite_preview"
    };

    /// <summary>
    /// 各平台可用模型目录
    /// </summary>
    /// <summary>
    /// 各平台可用模型目录
    /// </summary>
    private static readonly Dictionary<Provider, IReadOnlyList<ModelOption>> ModelCatalog = new()
    {
        [Provider.Antigravity] =
        [
            // Claude 4.6 系列
            new("Claude 4.6 Opus (Thinking)", "claude-opus-4-6-thinking"),
            new("Claude 4.6 Opus", "claude-opus-4-6"),
            new("Claude 4.6 Sonnet", "claude-sonnet-4-6"),
            // Gemini 3.1 系列
            new("Gemini 3.1 Pro High", "gemini-3.1-pro-high"),
            new("Gemini 3.1 Pro Low", "gemini-3.1-pro-low"),
            new("Gemini 3.1 Flash Image", "gemini-3.1-flash-image"),
            // Gemini 3 系列
            new("Gemini 3 Flash", "gemini-3-flash"),
            // Gemini 2.5 系统
            new("Gemini 2.5 Flash Lite", "gemini-2.5-flash-lite")
        ],
        [Provider.Gemini] =
        [
            new("Gemini 3.1 Pro Preview", "gemini-3.1-pro-preview"),
            new("Gemini 3.0 Pro Preview", "gemini-3-pro-preview"),
            new("Gemini 3.0 Flash Preview", "gemini-3-flash-preview"),
            new("Gemini 2.5 Pro", "gemini-2.5-pro"),
            new("Gemini 2.5 Flash", "gemini-2.5-flash"),
            new("Gemini 2.5 Flash Lite", "gemini-2.5-flash-lite"),
            new("Gemini 2.0 Flash", "gemini-2.0-flash")
        ],
        [Provider.Claude] =
        [
            new("Claude Opus 4.6", "claude-opus-4-6"),
            new("Claude Sonnet 4.6", "claude-sonnet-4-6"),
            new("Claude Opus 4.5", "claude-opus-4-5-20251101"),
            new("Claude Sonnet 4.5", "claude-sonnet-4-5-20250929"),
            new("Claude Haiku 4.5", "claude-haiku-4-5-20251001")
        ],
        [Provider.OpenAI] =
        [
            new("GPT-5.4", "gpt-5.4"),
            new("GPT-5.3 Codex", "gpt-5.3-codex"),
            new("GPT-5.3 Codex Spark", "gpt-5.3-codex-spark"),
            new("GPT-5.2", "gpt-5.2"),
            new("GPT-5.2 Codex", "gpt-5.2-codex"),
            new("GPT-5.1 Codex Max", "gpt-5.1-codex-max"),
            new("GPT-5.1 Codex", "gpt-5.1-codex"),
            new("GPT-5.1", "gpt-5.1"),
            new("GPT-5.1 Codex Mini", "gpt-5.1-codex-mini"),
            new("GPT-5", "gpt-5")
        ]
    };

    public string GetMappedModel(Provider provider, string requestedModel) => provider switch
    {
        Provider.Antigravity     => GetAntigravityMappedModel(requestedModel),
        Provider.Claude          => GetClaudeMappedModel(requestedModel),
        Provider.OpenAI          => GetOpenAIMappedModel(requestedModel),
        Provider.OpenAICompatible => requestedModel, // 三方兼容接口全透传，防止被 Codex fallback 强制降级为 gpt-5.1
        _                        => requestedModel // Gemini 等无需平台映射的提供商透传
    };

    private string GetAntigravityMappedModel(string requestedModel)
    {
        // 合并映射表（用户自定义 + 系统内置）
        var allMappings = new Dictionary<string, string>(CustomModelMappings);
        foreach (var (key, value) in AntigravityModelMappings)
        {
            allMappings.TryAdd(key, value); // 用户自定义优先
        }

        // 1. 精确匹配（最高优先级）
        if (allMappings.TryGetValue(requestedModel, out var exactMatch))
        {
            logger.LogDebug("精确映射：{RequestedModel} -> {MappedModel}", requestedModel, exactMatch);
            return exactMatch;
        }

        // 2. 通配符匹配（按特异性评分）
        string? bestMatchTarget = null;
        var bestSpecificity = -1;

        foreach (var (pattern, target) in allMappings)
        {
            if (pattern.Contains('*') && WildcardMatch(pattern, requestedModel))
            {
                var specificity = CalculateSpecificity(pattern);
                if (specificity > bestSpecificity)
                {
                    bestMatchTarget = target;
                    bestSpecificity = specificity;
                }
            }
        }

        if (bestMatchTarget is not null)
        {
            logger.LogDebug(
                "通配符匹配：{RequestedModel} -> {MappedModel} (特异性: {Specificity})",
                requestedModel,
                bestMatchTarget,
                bestSpecificity);
            return bestMatchTarget;
        }

        // 3. 系统默认映射（透传 + 降级策略）
        return ApplyDefaultMapping(requestedModel);
    }

    private string GetOpenAIMappedModel(string requestedModel)
    {
        if (string.IsNullOrWhiteSpace(requestedModel))
            return "gpt-5.1";

        // 精确匹配
        if (OpenAICodexModelMappings.TryGetValue(requestedModel, out var mapped))
        {
            return mapped;
        }

        // 模糊匹配
        var lower = requestedModel.ToLowerInvariant();
        if (lower.Contains("5.3")) return "gpt-5.3-codex";
        if (lower.Contains("5.2-codex")) return "gpt-5.2-codex";
        if (lower.Contains("5.2")) return "gpt-5.2";
        if (lower.Contains("5.1-codex-max")) return "gpt-5.1-codex-max";
        if (lower.Contains("5.1-codex-mini") || lower.Contains("codex-mini")) return "gpt-5.1-codex-mini";
        if (lower.Contains("5.1-codex")) return "gpt-5.1-codex";
        if (lower.Contains("5.1")) return "gpt-5.1";
        if (lower.Contains("codex")) return "gpt-5.1-codex";
        if (lower.Contains("gpt-5") || lower.Contains("gpt 5")) return "gpt-5.1";

        return requestedModel;
    }

    private string GetClaudeMappedModel(string requestedModel)
    {
        if (string.IsNullOrWhiteSpace(requestedModel))
            return requestedModel;

        // 精确匹配
        if (ClaudeModelMappings.TryGetValue(requestedModel, out var mapped))
        {
            logger.LogDebug("Claude 模型映射：{RequestedModel} -> {MappedModel}", requestedModel, mapped);
            return mapped;
        }

        // 无匹配则透传
        return requestedModel;
    }

    public IReadOnlyList<ModelOption> GetAvailableModels(Provider provider)
    {
        return ModelCatalog.TryGetValue(provider, out var models)
            ? models
            : Array.Empty<ModelOption>();
    }

    /// <summary>
    /// 应用系统默认映射策略
    /// </summary>
    private string ApplyDefaultMapping(string requestedModel)
    {
        // 3.1 透传策略：gemini- 开头或包含 thinking 的模型直接透传
        if (requestedModel.StartsWith("gemini-", StringComparison.Ordinal) ||
            requestedModel.Contains("thinking", StringComparison.Ordinal))
        {
            logger.LogDebug("透传模型：{RequestedModel}", requestedModel);
            return requestedModel;
        }

        // 3.2 智能降级：opus 模型降级到 gemini-3-pro-preview
        if (requestedModel.Contains("opus", StringComparison.Ordinal))
        {
            logger.LogDebug("Opus 模型降级：{RequestedModel} -> gemini-3-pro-preview", requestedModel);
            return "gemini-3-pro-preview";
        }

        // 3.3 默认降级：未知模型降级到 claude-sonnet-4-5
        logger.LogWarning("未知模型，使用默认：{RequestedModel} -> claude-sonnet-4-5", requestedModel);
        return "claude-sonnet-4-5";
    }

    /// <summary>
    /// 通配符匹配算法（支持多个通配符）
    /// </summary>
    private static bool WildcardMatch(string pattern, string text)
    {
        var parts = pattern.Split('*');

        // 无通配符 - 精确匹配
        if (parts.Length == 1)
        {
            return pattern == text;
        }

        var textPos = 0;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            // 跳过空片段（来自连续的通配符）
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (i == 0)
            {
                // 第一个片段必须匹配开头
                if (!text[textPos..].StartsWith(part, StringComparison.Ordinal))
                {
                    return false;
                }
                textPos += part.Length;
            }
            else if (i == parts.Length - 1)
            {
                // 最后一个片段必须匹配结尾
                return text[textPos..].EndsWith(part, StringComparison.Ordinal);
            }
            else
            {
                // 中间片段 - 查找下一个出现位置
                var index = text.IndexOf(part, textPos, StringComparison.Ordinal);
                if (index == -1)
                {
                    return false;
                }
                textPos = index + part.Length;
            }
        }

        return true;
    }

    /// <summary>
    /// 计算通配符模式的特异性评分
    /// </summary>
    private static int CalculateSpecificity(string pattern)
    {
        return pattern.Length - pattern.Count(c => c == '*');
    }
}

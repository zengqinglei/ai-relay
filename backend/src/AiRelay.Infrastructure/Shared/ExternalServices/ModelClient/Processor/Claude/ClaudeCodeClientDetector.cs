using System.Text.RegularExpressions;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Claude;

/// <summary>
/// 检测请求是否来自 Claude Code 客户端
/// </summary>
public class ClaudeCodeClientDetector : IClaudeCodeClientDetector
{
    private static readonly Regex ClaudeCodeUAPattern =
        new(@"^claude-cli/\d+\.\d+\.\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UserIdPattern =
        new(@"^user_[a-fA-F0-9]{64}_account__session_[\w-]+$", RegexOptions.Compiled);

    private const double SystemPromptThreshold = 0.5;

    private static readonly string[] ClaudeCodeSystemPrompts =
    [
        "You are Claude Code, Anthropic's official CLI for Claude.",
        "You are a Claude agent, built on Anthropic's Claude Agent SDK.",
        "You are Claude Code, Anthropic's official CLI for Claude, running within the Claude Agent SDK.",
        "You are a file search specialist for Claude Code, Anthropic's official CLI for Claude.",
        "You are a helpful AI assistant tasked with summarizing conversations.",
        "You are an interactive CLI tool that helps users"
    ];

    public bool IsClaudeCodeClient(DownRequestContext down)
    {
        var userAgent = down.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent) || !ClaudeCodeUAPattern.IsMatch(userAgent))
            return false;

        var isMessagesPath = down.RelativePath?.Contains("messages", StringComparison.OrdinalIgnoreCase) == true;
        if (!isMessagesPath)
            return true;

        return HasClaudeCodeSystemPrompt(down) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("x-app")) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("anthropic-beta")) &&
               !string.IsNullOrEmpty(down.Headers.GetValueOrDefault("anthropic-version")) &&
               ValidateMetadataUserId(down);
    }

    private static bool HasClaudeCodeSystemPrompt(DownRequestContext down)
    {
        for (int i = 0; i < 5; i++)
        {
            if (down.ExtractedProps.TryGetValue($"claude.sys_text_{i}", out var text) &&
                !string.IsNullOrEmpty(text) &&
                BestSimilarityScore(text) >= SystemPromptThreshold)
            {
                return true;
            }
        }
        return false;
    }

    private static bool ValidateMetadataUserId(DownRequestContext down)
    {
        return down.ExtractedProps.TryGetValue("claude.metadata_user_id", out var userId) &&
               !string.IsNullOrEmpty(userId) &&
               UserIdPattern.IsMatch(userId);
    }

    private static double BestSimilarityScore(string text)
    {
        var normalizedText = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var bestScore = 0.0;

        foreach (var template in ClaudeCodeSystemPrompts)
        {
            var normalizedTemplate = string.Join(" ", template.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var score = DiceCoefficient(normalizedText, normalizedTemplate);
            if (score > bestScore) bestScore = score;
        }
        return bestScore;
    }

    private static double DiceCoefficient(string a, string b)
    {
        if (a == b) return 1.0;
        if (a.Length < 2 || b.Length < 2) return 0.0;

        var bigramsA = GetBigrams(a);
        var bigramsB = GetBigrams(b);
        if (bigramsA.Count == 0 || bigramsB.Count == 0) return 0.0;

        var intersection = bigramsA.Sum(kvp =>
            bigramsB.TryGetValue(kvp.Key, out var countB) ? Math.Min(kvp.Value, countB) : 0);
        return 2.0 * intersection / (bigramsA.Values.Sum() + bigramsB.Values.Sum());
    }

    private static Dictionary<string, int> GetBigrams(string s)
    {
        var bigrams = new Dictionary<string, int>();
        var lower = s.ToLowerInvariant();
        for (var i = 0; i < lower.Length - 1; i++)
        {
            var bigram = lower.Substring(i, 2);
            bigrams[bigram] = bigrams.GetValueOrDefault(bigram) + 1;
        }
        return bigrams;
    }
}

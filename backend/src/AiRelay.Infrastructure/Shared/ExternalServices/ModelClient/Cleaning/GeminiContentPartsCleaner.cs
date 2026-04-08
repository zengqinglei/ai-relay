using System.Text.Json.Nodes;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

/// <summary>
/// Gemini content parts pre-processing: empty parts filtering and functionCall signature enforcement
/// Ref: sub2api filterEmptyPartsFromGeminiRequest + ensureGeminiFunctionCallThoughtSignatures
/// </summary>
public static class GeminiContentPartsCleaner
{
    /// <summary>
    /// Remove content entries with empty parts arrays from contents[] and systemInstruction.
    /// Gemini API rejects parts: [] with 400 INVALID_ARGUMENT.
    /// Ref: sub2api filterEmptyPartsFromGeminiRequest
    /// </summary>
    public static void FilterEmptyParts(JsonObject body)
    {
        // Process contents[]
        if (body["contents"] is JsonArray contents)
        {
            for (var i = contents.Count - 1; i >= 0; i--)
            {
                if (contents[i] is JsonObject entry &&
                    entry["parts"] is JsonArray parts &&
                    parts.Count == 0)
                {
                    contents.RemoveAt(i);
                }
            }
        }

        // Process systemInstruction
        if (body["systemInstruction"] is JsonObject sysInst &&
            sysInst["parts"] is JsonArray sysParts &&
            sysParts.Count == 0)
        {
            body.Remove("systemInstruction");
        }
    }

    /// <summary>
    /// Ensure functionCall parts in contents[] have a thoughtSignature field.
    /// Some upstream endpoints (Code Assist) strictly validate this field,
    /// missing it causes 400 INVALID_ARGUMENT.
    /// </summary>
    /// <param name="body">Request body JSON object</param>
    /// <param name="defaultSignature">Default signature value (from cache). Null falls back to a known bypass value.</param>
    public static void EnsureFunctionCallThoughtSignatures(JsonObject body, string? defaultSignature)
    {
        if (body["contents"] is not JsonArray contents) return;

        foreach (var contentNode in contents)
        {
            if (contentNode is not JsonObject content) continue;
            if (content["parts"] is not JsonArray parts) continue;

            foreach (var partNode in parts)
            {
                if (partNode is not JsonObject part) continue;
                if (!part.ContainsKey("functionCall")) continue;
                if (part.ContainsKey("thoughtSignature")) continue;

                // Code Assist validates this field strictly; empty string is rejected.
                // "skip_thought_signature_validator" is a known bypass value (ref: sub2api).
                part["thoughtSignature"] = defaultSignature ?? "skip_thought_signature_validator";
            }
        }
    }
}

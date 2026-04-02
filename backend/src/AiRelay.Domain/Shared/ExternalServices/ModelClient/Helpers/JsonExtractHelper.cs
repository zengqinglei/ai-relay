using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Helpers;

public static partial class JsonExtractHelper
{
    [GeneratedRegex(@"\.gemini/tmp/([A-Fa-f0-9]{64})", RegexOptions.Compiled)]
    private static partial Regex GeminiCliTmpDirRegex();

    /// <summary>
    /// 高性能前置流扫描（0分配）
    /// 仅扫描流的前N个字节，提取所有顶层基础类型属性及其子对象的基础属性（如 messages[0].content）
    /// 并同时生成供日志使用的主体内容预览字符串，避免二次读取引发崩溃
    /// </summary>
    public static async Task<(Dictionary<string, string> Props, string? BodyPreview)> ExtractEssentialPropsAsync(Stream? stream, bool needPreview = false, int previewMaxLength = 2000, int maxBytesToRead = 262144)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? bodyPreview = null;
        if (stream == null || stream == Stream.Null || !stream.CanRead)
            return (result, bodyPreview);

        long originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            // 租用少量缓冲池读取前部流
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytesToRead);
            try
            {
                // 循环读满 maxBytesToRead 或流结束，避免单次 ReadAsync 只读取部分数据
                int bytesRead = 0;
                int remaining = maxBytesToRead;
                while (remaining > 0)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(bytesRead, remaining));
                    if (read == 0) break;
                    bytesRead += read;
                    remaining -= read;
                }
                if (bytesRead == 0) return (result, bodyPreview);

                // 根据设定生成预览字符串
                if (needPreview)
                {
                    var snippet = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    bodyPreview = snippet.Length <= previewMaxLength ? snippet : snippet[..previewMaxLength] + "...";
                }

                var reader = new Utf8JsonReader(buffer.AsSpan(0, bytesRead));
                
                int depth = 0;
                string? currentTopLevelPropName = null;
                string? currentPropName = null;

                // cache_control ephemeral tracking for system[] and messages[] items
                string? currentEphemeralText = null;
                bool currentObjectHasCacheControl = false;
                int currentCacheObjectDepth = 0;

                // user role count for PromptIndex (Gemini ExtractPromptIndex)
                int userRoleCount = 0;
                int systemTextIdx = 0;

                // Gemini CLI prompt detection: track if we're inside systemInstruction
                bool insideSystemInstruction = false;

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                        case JsonTokenType.StartArray:
                            depth++;
                            // Check if entering a cacheable object (depth 3 for system[], depth 5 for messages[x].content[])
                            if ((currentTopLevelPropName == "system" && depth == 3) || 
                                (currentTopLevelPropName == "messages" && depth == 5))
                            {
                                currentEphemeralText = null;
                                currentObjectHasCacheControl = false;
                                currentCacheObjectDepth = depth;
                            }
                            break;

                        case JsonTokenType.EndObject:
                        case JsonTokenType.EndArray:
                            // Commit text if cache_control was present in this object
                            if (currentCacheObjectDepth > 0 && depth == currentCacheObjectDepth && reader.TokenType == JsonTokenType.EndObject)
                            {
                                if (currentObjectHasCacheControl && !string.IsNullOrWhiteSpace(currentEphemeralText)
                                    && !result.ContainsKey("cache_ephemeral_text"))
                                {
                                    result["cache_ephemeral_text"] = currentEphemeralText;
                                }
                                currentCacheObjectDepth = 0;
                                currentObjectHasCacheControl = false;
                                currentEphemeralText = null;
                            }
                            depth--;
                            if (depth == 1) currentTopLevelPropName = null;
                            // Exit systemInstruction context when leaving depth 1
                            if (depth == 1 && insideSystemInstruction) insideSystemInstruction = false;
                            break;

                            case JsonTokenType.PropertyName:
                            currentPropName = reader.GetString();
                            if (depth == 1)
                            {
                                currentTopLevelPropName = currentPropName;

                                // Track systemInstruction context for Gemini CLI detection
                                if (currentPropName == "systemInstruction")
                                {
                                    insideSystemInstruction = true;
                                }

                                // Top-level struct flags (for zero-allocation shortcut detection)
                                if (!result.ContainsKey("has_v1internal_request") && currentPropName == "request") result["has_v1internal_request"] = "true";
                                else if (!result.ContainsKey("has_v1internal_project") && currentPropName == "project") result["has_v1internal_project"] = "true";
                                else if (!result.ContainsKey("has_openai_input") && currentPropName == "input") result["has_openai_input"] = "true";
                                else if (!result.ContainsKey("has_openai_messages") && currentPropName == "messages") result["has_openai_messages"] = "true";
                            }
                            
                            // Track "tools" existence (can be at depth 1 or depth 2 when inside v1internal "request")
                            if (depth <= 2 && currentPropName == "tools" && !result.ContainsKey("has_tools"))
                            {
                                result["has_tools"] = "true";
                            }

                            // Track cache_control property in system[] and messages[] items
                            if (currentCacheObjectDepth > 0 && depth == currentCacheObjectDepth && currentPropName == "cache_control")
                            {
                                currentObjectHasCacheControl = true;
                            }
                            break;

                        case JsonTokenType.String:
                        case JsonTokenType.Number:
                        case JsonTokenType.True:
                        case JsonTokenType.False:
                            if (depth == 1 && currentTopLevelPropName != null)
                            {
                                if (!result.ContainsKey(currentTopLevelPropName))
                                {
                                    result[currentTopLevelPropName] = reader.TokenType == JsonTokenType.String 
                                        ? reader.GetString()! 
                                        : GetRawString(reader);
                                }
                            }
                            else if (currentTopLevelPropName == "messages" && currentPropName == "content")
                            {
                                if (!result.ContainsKey("messages[0].content") && reader.TokenType == JsonTokenType.String)
                                {
                                    result["messages[0].content"] = reader.GetString()!;
                                }
                            }
                            else if (currentTopLevelPropName == "metadata" && currentPropName == "user_id")
                            {
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    result["metadata.user_id"] = reader.GetString()!;
                                }
                            }
                            else if (currentTopLevelPropName == "contents" && currentPropName == "text")
                            {
                                if (!result.ContainsKey("messages[0].content") && reader.TokenType == JsonTokenType.String)
                                {
                                    result["messages[0].content"] = reader.GetString()!;
                                }
                            }
                            // Count user roles in contents[]/messages[] for PromptIndex
                            else if ((currentTopLevelPropName is "contents" or "messages") && currentPropName == "role"
                                     && depth == 3 && reader.TokenType == JsonTokenType.String
                                     && reader.ValueTextEquals("user"u8))
                            {
                                userRoleCount++;
                            }
                            else if (currentTopLevelPropName == "request" && currentPropName == "session_id")
                            {
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    result["request.session_id"] = reader.GetString()!;
                                }
                            }

                            // Extract text from items with cache_control (Claude Prompt Caching)
                            if (currentCacheObjectDepth > 0 && depth == currentCacheObjectDepth && currentPropName == "text" && reader.TokenType == JsonTokenType.String)
                            {
                                currentEphemeralText ??= reader.GetString();
                                
                                // 提取短系统提示词供 ClaudeCodeClientDetector 进行零分配检测（通常特征词 < 1500 且最多提取5个）
                                if (currentTopLevelPropName == "system" && reader.ValueSpan.Length <= 1500)
                                {
                                    if (systemTextIdx < 5)
                                    {
                                        result[$"system_text_{systemTextIdx}"] = currentEphemeralText ?? string.Empty;
                                        systemTextIdx++;
                                    }
                                }
                            }

                            // 探测 Gemini CLI Prompt（仅在 systemInstruction 上下文中）
                            if (insideSystemInstruction && currentPropName == "text" && reader.TokenType == JsonTokenType.String && !result.ContainsKey("is_gemini_cli_prompt"))
                            {
                                if (reader.ValueSpan.IndexOf("Gemini CLI"u8) >= 0)
                                {
                                    result["is_gemini_cli_prompt"] = "true";
                                }
                            }

                            // 合并无损正则嗅探逻辑：在单次循环中顺手用 ReadOnlySpan 探测 .gemini/tmp/ 的签名（Gemini CLI 专属）
                            // 避免了重新分配 50KB~100KB 的大字符串用于正则匹配，防止 LOH 触发
                            if (reader.TokenType == JsonTokenType.String && !result.ContainsKey("gemini_cli_tmp_hash"))
                            {
                                var valSpan = reader.ValueSpan;
                                // 查找 ".gemini/tmp/"，SIMD 加速的 Span<byte> IndexOf
                                if (valSpan.IndexOf(".gemini/tmp/"u8) >= 0)
                                {
                                    var strVal = reader.GetString();
                                    if (strVal != null)
                                    {
                                        var match = GeminiCliTmpDirRegex().Match(strVal);
                                        if (match.Success)
                                        {
                                            result["gemini_cli_tmp_hash"] = match.Groups[1].Value;
                                        }
                                    }
                                }
                            }
                            break;
                    }
                }

                // Write user_role_count if any user roles were found
                if (userRoleCount > 0)
                {
                    result["user_role_count"] = userRoleCount.ToString();
                }
            }
            catch (JsonException)
            {
                // 如果截断导致 JSON 不完整，会报 JsonException，忽略即可
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch
        {
            // 忽略读取异常
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }

        return (result, bodyPreview);
    }

    private static string GetRawString(Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt64(out var longVal) ? longVal.ToString() : reader.GetDouble().ToString(),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => string.Empty
        };
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Helpers;

public static partial class JsonExtractHelper
{
    [GeneratedRegex(@"\.gemini[/\\]tmp[/\\]([A-Fa-f0-9]{64})", RegexOptions.Compiled)]
    private static partial Regex GeminiCliTmpDirRegex();

    /// <summary>
    /// 高性能前置流扫描（0分配）
    /// 仅扫描流的前N个字节，提取所有顶层基础类型属性及其子对象的基础属性（如 messages[0].content）
    /// 并同时生成供日志使用的主体内容预览字符串，避免二次读取引发崩溃
    /// </summary>
    public static async Task<(Dictionary<string, string> Props, string? BodyPreview)> ExtractEssentialPropsAsync(Stream? stream, bool needPreview = false, int previewMaxLength = 2000, int maxBytesToRead = 1048576) // [优化] 提升至 1MB 缓冲区以支持大报文探测
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

                // 为 system[] 和 messages[] 项目进行 cache_control 临时跟踪
                string? currentEphemeralText = null;
                bool currentObjectHasCacheControl = false;
                int currentCacheObjectDepth = 0;

                // 为 PromptIndex (Gemini ExtractPromptIndex) 统计用户角色数量
                int userRoleCount = 0;
                int systemTextIdx = 0;
                bool isNextUserContent = false;
                bool hasSetFingerprint = false;
                bool hasSavedDefault = false;

                // Gemini CLI prompt detection: track if we're inside systemInstruction
                bool insideSystemInstruction = false;

                // Platform hint: track if we're inside systemInstruction.parts to detect Antigravity identity
                bool insideSysInstParts = false;

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                        case JsonTokenType.StartArray:
                            depth++;
                            // 检查是否进入可缓存对象（system[] 深度为 3，messages[x].content[] 深度为 5）
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
                            // 如果该对象中存在 cache_control，则提交文本
                            if (currentCacheObjectDepth > 0 && depth == currentCacheObjectDepth && reader.TokenType == JsonTokenType.EndObject)
                            {
                                if (currentObjectHasCacheControl && !string.IsNullOrWhiteSpace(currentEphemeralText)
                                    && !result.ContainsKey("claude.cache_text"))
                                {
                                    result["claude.cache_text"] = currentEphemeralText;
                                }
                                currentCacheObjectDepth = 0;
                                currentObjectHasCacheControl = false;
                                currentEphemeralText = null;
                            }
                            depth--;
                            if (depth == 1) currentTopLevelPropName = null;
                            // 离开深度 1 时退出 systemInstruction 上下文
                            if (depth == 1 && insideSystemInstruction)
                            {
                                insideSystemInstruction = false;
                                insideSysInstParts = false;
                            }
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
                                    // 平台标识：顶层含 systemInstruction → Gemini 原生报文
                                    result.TryAdd("google.is_gemini", "true");
                                }

                                // 平台标识：顶层含 conversation_id → Antigravity 特有字段
                                if (currentPropName == "conversation_id")
                                    result.TryAdd("google.antigravity_session", "true");

                                // 顶层结构标志（用于零分配快捷路径探测）
                                if (!result.ContainsKey("google.has_v1internal") && currentPropName == "request") result["google.has_v1internal"] = "true";
                                else if (!result.ContainsKey("google.has_project") && currentPropName == "project") result["google.has_project"] = "true";
                                else if (!result.ContainsKey("openai.has_input") && currentPropName == "input") result["openai.has_input"] = "true";
                                else if (!result.ContainsKey("openai.has_messages") && currentPropName == "messages") result["openai.has_messages"] = "true";
                            }

                            // 平台标识：进入 systemInstruction.parts 时设置 insideSysInstParts 标志
                            if (insideSystemInstruction && depth == 3 && currentPropName == "parts")
                                insideSysInstParts = true;
                            else if (!insideSystemInstruction)
                                insideSysInstParts = false;
                            
                             // 追踪 "tools" 的存在情况（在 v1internal "request" 内部时深度可能为 1 或 2）
                            if (depth <= 2 && currentPropName == "tools" && !result.ContainsKey("public.has_tools"))
                            {
                                result["public.has_tools"] = "true";
                            }

                            // 追踪 system[] 和 messages[] 项目中的 cache_control 属性
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
                                // [标准化] 为公共顶层属性映射 'public.' 前缀
                                var finalKey = currentTopLevelPropName switch
                                {
                                    "model" => "public.model",
                                    "stream" => "public.stream",
                                    "conversation_id" or "session_id" => "public.conversation_id",
                                    _ => currentTopLevelPropName
                                };

                                if (!result.ContainsKey(finalKey))
                                {
                                    result[finalKey] = reader.TokenType == JsonTokenType.String 
                                        ? reader.GetString()! 
                                        : GetRawString(reader);
                                }
                            }
                            else if ((currentTopLevelPropName is "messages" or "contents" or "input") && (currentPropName is "content" or "text"))
                            {
                                if (!hasSetFingerprint && reader.TokenType == JsonTokenType.String)
                                {
                                    var contentValue = reader.GetString()!;
                                    if (isNextUserContent)
                                    {
                                        result["public.fingerprint"] = contentValue;
                                        hasSetFingerprint = true; // 锁定 User 内容
                                    }
                                    else if (!hasSavedDefault)
                                    {
                                        result["public.fingerprint"] = contentValue;
                                        hasSavedDefault = true; // 暂存第一条内容做保底
                                    }
                                }
                            }
                            else if (currentTopLevelPropName == "metadata" && currentPropName == "user_id")
                            {
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    result["claude.metadata_user_id"] = reader.GetString()!;
                                }
                            }
                            else if (currentTopLevelPropName == "stream_options" && currentPropName == "include_usage")
                            {
                                if (reader.TokenType == JsonTokenType.True)
                                {
                                    result["openai.include_usage"] = "true";
                                }
                            }
                            // Count user roles in contents[]/messages[]/input[] for PromptIndex
                            else if ((currentTopLevelPropName is "contents" or "messages" or "input") && currentPropName == "role"
                                     && depth == 3 && reader.TokenType == JsonTokenType.String
                                     && reader.ValueTextEquals("user"u8))
                            {
                                userRoleCount++;
                                isNextUserContent = true;
                            }
                            else if (depth == 1 && currentPropName == "user_prompt_id")
                            {
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    result["google.user_prompt_id"] = reader.GetString()!;
                                }
                            }
                            else if (currentTopLevelPropName == "request" && currentPropName == "session_id")
                            {
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    result["google.request_session_id"] = reader.GetString()!;
                                }
                            }

                            // 从带有 cache_control 的项目中提取文本（Claude Prompt 缓存）
                            if (currentCacheObjectDepth > 0 && depth == currentCacheObjectDepth && currentPropName == "text" && reader.TokenType == JsonTokenType.String)
                            {
                                currentEphemeralText ??= reader.GetString();
                                
                                // 提取短系统提示词供 ClaudeCodeClientDetector 进行零分配检测（通常特征词 < 1500 且最多提取5个）
                                if (currentTopLevelPropName == "system" && reader.ValueSpan.Length <= 1500)
                                {
                                    if (systemTextIdx < 5)
                                    {
                                        result[$"claude.sys_text_{systemTextIdx}"] = currentEphemeralText ?? string.Empty;
                                        systemTextIdx++;
                                    }
                                }
                            }

                            // 探测 Gemini CLI Prompt 及 Antigravity 身份（仅在 systemInstruction 上下文中）
                            if (insideSystemInstruction && currentPropName == "text" && reader.TokenType == JsonTokenType.String)
                            {
                                if (!result.ContainsKey("google.is_cli") && reader.ValueSpan.IndexOf("Gemini CLI"u8) >= 0)
                                    result["google.is_cli"] = "true";

                                // 平台标识：systemInstruction 的 parts.text 含 "Antigravity" → 身份已注入，无需重复注入
                                if (insideSysInstParts && !result.ContainsKey("google.has_identity") && reader.ValueSpan.IndexOf("Antigravity"u8) >= 0)
                                    result["google.has_identity"] = "true";
                            }

                            // 合并无损正则嗅探逻辑：在单次循环中顺手用 ReadOnlySpan 探测 .gemini/tmp/ 的签名（Gemini CLI 专属）
                            // 避免了重新分配 50KB~100KB 的大字符串用于正则匹配，防止 LOH 触发
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                var valSpan = reader.ValueSpan;

                                // 平台标识：任意字符串含 thoughtSignature 字段名 → 签名已存在于请求体
                                if (!result.ContainsKey("google.has_signature") && valSpan.IndexOf("thoughtSignature"u8) >= 0)
                                    result["google.has_signature"] = "true";

                                // 查找 ".gemini" 关键字，支持 Windows (\) 和 Linux (/) 路径
                                if (!result.ContainsKey("google.cli_tmp_hash") && valSpan.IndexOf(".gemini"u8) >= 0)
                                {
                                    var strVal = reader.GetString();
                                    if (strVal != null)
                                    {
                                        var match = GeminiCliTmpDirRegex().Match(strVal);
                                        if (match.Success)
                                            result["google.cli_tmp_hash"] = match.Groups[1].Value;
                                    }
                                }
                            }
                            break;
                    }
                }

                // 如果发现了任何用户角色，记录 user_role_count
                if (userRoleCount > 0)
                {
                    result["public.user_role_count"] = userRoleCount.ToString();
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

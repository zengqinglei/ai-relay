using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Cleaning;

/// <summary>
/// Google JSON Schema 清洗器
/// 用于将复杂的 JSON Schema (Draft 2020-12) 降级为 Gemini/Antigravity 支持的简单格式
/// </summary>
public class GoogleJsonSchemaCleaner(ILogger<GoogleJsonSchemaCleaner> logger)
{
    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "description",
        "properties",
        "required",
        "items",
        "enum",
        "title"
    };

    private static readonly Dictionary<string, string> ConstraintMap = new()
    {
        { "minLength", "minLen" },
        { "maxLength", "maxLen" },
        { "pattern", "pattern" },
        { "minimum", "min" },
        { "maximum", "max" },
        { "multipleOf", "multipleOf" },
        { "exclusiveMinimum", "exclMin" },
        { "exclusiveMaximum", "exclMax" },
        { "minItems", "minItems" },
        { "maxItems", "maxItems" },
        { "propertyNames", "propertyNames" },
        { "format", "format" }
    };

    public void Clean(JsonNode? schema)
    {
        if (schema is not JsonObject root) return;

        // 0. 预处理：收集并展开 $defs/$definitions
        var defs = new Dictionary<string, JsonNode>();
        CollectAllDefs(root, defs);

        // 移除根节点的 definitions
        root.Remove("$defs");
        root.Remove("definitions");

        FlattenRefs(root, defs);

        // 递归清洗
        CleanRecursive(root);
    }

    private void CollectAllDefs(JsonNode node, Dictionary<string, JsonNode> defs)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("$defs", out var d) && d is JsonObject dObj)
            {
                foreach (var kv in dObj)
                {
                    if (kv.Value != null && !defs.ContainsKey(kv.Key))
                        defs[kv.Key] = kv.Value.DeepClone();
                }
            }
            if (obj.TryGetPropertyValue("definitions", out var defsNode) && defsNode is JsonObject defsObj)
            {
                foreach (var kv in defsObj)
                {
                    if (kv.Value != null && !defs.ContainsKey(kv.Key))
                        defs[kv.Key] = kv.Value.DeepClone();
                }
            }

            foreach (var kv in obj)
            {
                if (kv.Key != "$defs" && kv.Key != "definitions" && kv.Value != null)
                {
                    CollectAllDefs(kv.Value, defs);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null) CollectAllDefs(item, defs);
            }
        }
    }

    private void FlattenRefs(JsonNode node, Dictionary<string, JsonNode> defs)
    {
        if (node is JsonObject obj)
        {
            // 处理 $ref
            if (obj.ContainsKey("$ref"))
            {
                var refPath = obj["$ref"]?.GetValue<string>();
                obj.Remove("$ref");

                if (!string.IsNullOrEmpty(refPath))
                {
                    var refName = refPath.Split('/').Last();
                    if (defs.TryGetValue(refName, out var defNode) && defNode is JsonObject defObj)
                    {
                        // 合并定义
                        foreach (var kv in defObj)
                        {
                            if (!obj.ContainsKey(kv.Key))
                            {
                                obj[kv.Key] = kv.Value?.DeepClone();
                            }
                        }
                        // 递归处理合并进来的内容
                        FlattenRefs(obj, defs);
                    }
                    else
                    {
                        // 无法解析的引用，降级为 string
                        obj["type"] = "string";
                        AppendDescription(obj, $"(Unresolved $ref: {refPath})");
                        logger.LogWarning("无法解析的 schema 引用: {RefPath}", refPath);
                    }
                }
            }

            // 递归子节点
            foreach (var kv in obj.ToList()) // ToList to avoid modification during iteration
            {
                if (kv.Value != null) FlattenRefs(kv.Value, defs);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null) FlattenRefs(item, defs);
            }
        }
    }

    private bool CleanRecursive(JsonNode node)
    {
        if (node is not JsonObject obj) return false;

        bool isEffectivelyNullable = false;

        // 0. 合并 allOf
        MergeAllOf(obj);

        // 1. 递归处理子项
        if (obj["properties"] is JsonObject props)
        {
            var nullableKeys = new HashSet<string>();
            foreach (var kv in props)
            {
                if (kv.Value != null && CleanRecursive(kv.Value))
                {
                    nullableKeys.Add(kv.Key);
                }
            }

            // 处理 required
            if (nullableKeys.Count > 0 && obj["required"] is JsonArray reqArr)
            {
                var newReq = new JsonArray();
                foreach (var r in reqArr)
                {
                    var name = r?.GetValue<string>();
                    if (name != null && !nullableKeys.Contains(name))
                    {
                        newReq.Add(r?.DeepClone());
                    }
                }
                if (newReq.Count > 0)
                    obj["required"] = newReq;
                else
                    obj.Remove("required");
            }
        }
        else if (obj["items"] != null)
        {
            var items = obj["items"];
            if (items is JsonArray itemsArr)
            {
                // 元组验证 -> 列表验证 (取最佳匹配)
                var best = ExtractBestSchemaFromUnion(itemsArr);
                if (best != null)
                {
                    CleanRecursive(best);
                    obj["items"] = best;
                }
                else
                {
                    obj["items"] = new JsonObject { ["type"] = "string" };
                }
            }
            else if (items != null)
            {
                CleanRecursive(items);
            }
        }
        else
        {
            foreach (var kv in obj)
            {
                if (kv.Value is JsonObject || kv.Value is JsonArray)
                {
                    if (kv.Value != null) CleanRecursive(kv.Value);
                }
            }
        }

        // 1.5 递归清理 anyOf/oneOf 分支
        if (obj["anyOf"] is JsonArray anyOf)
        {
            foreach (var item in anyOf) if (item != null) CleanRecursive(item);
        }
        if (obj["oneOf"] is JsonArray oneOf)
        {
            foreach (var item in oneOf) if (item != null) CleanRecursive(item);
        }

        // 2. 处理联合类型 (anyOf/oneOf) -> 合并
        JsonArray? unionToMerge = null;
        var typeStr = obj["type"]?.GetValue<string>();
        if (string.IsNullOrEmpty(typeStr) || typeStr == "object")
        {
            if (obj["anyOf"] is JsonArray ao) unionToMerge = ao;
            else if (obj["oneOf"] is JsonArray oo) unionToMerge = oo;
        }

        if (unionToMerge != null)
        {
            var best = ExtractBestSchemaFromUnion(unionToMerge);
            if (best is JsonObject bestObj)
            {
                // 合并属性
                foreach (var kv in bestObj)
                {
                    if (kv.Key == "properties" && kv.Value is JsonObject sourceProps)
                    {
                        if (obj["properties"] is not JsonObject targetProps)
                        {
                            targetProps = new JsonObject();
                            obj["properties"] = targetProps;
                        }
                        foreach (var pk in sourceProps)
                        {
                            if (!targetProps.ContainsKey(pk.Key))
                            {
                                targetProps[pk.Key] = pk.Value?.DeepClone();
                            }
                        }
                    }
                    else if (kv.Key == "required" && kv.Value is JsonArray sourceReq)
                    {
                        if (obj["required"] is not JsonArray targetReq)
                        {
                            targetReq = new JsonArray();
                            obj["required"] = targetReq;
                        }
                        var existing = targetReq.Select(x => x?.GetValue<string>()).ToHashSet();
                        foreach (var req in sourceReq)
                        {
                            var reqStr = req?.GetValue<string>();
                            if (reqStr != null && !existing.Contains(reqStr))
                            {
                                targetReq.Add(reqStr);
                                existing.Add(reqStr);
                            }
                        }
                    }
                    else if (!obj.ContainsKey(kv.Key))
                    {
                        obj[kv.Key] = kv.Value?.DeepClone();
                    }
                }
            }
        }

        // 3. 检查是否为 Schema 对象
        bool looksLikeSchema = obj.ContainsKey("type") || obj.ContainsKey("properties") ||
                               obj.ContainsKey("items") || obj.ContainsKey("enum") ||
                               obj.ContainsKey("anyOf") || obj.ContainsKey("oneOf") || obj.ContainsKey("allOf");

        if (looksLikeSchema)
        {
            // 4. 约束迁移
            MigrateConstraints(obj);

            // 5. 白名单过滤
            var keysToRemove = obj.Select(x => x.Key).Where(k => !AllowedFields.Contains(k)).ToList();
            foreach (var k in keysToRemove) obj.Remove(k);

            // 6. 空对象填充
            if (obj["type"]?.GetValue<string>() == "object")
            {
                if (obj["properties"] is not JsonObject objProps || objProps.Count == 0)
                {
                    obj["properties"] = new JsonObject
                    {
                        ["reason"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Reason for calling this tool"
                        }
                    };
                    obj["required"] = new JsonArray { "reason" };
                }
            }

            // 7. Required 对齐
            if (obj["properties"] is JsonObject validProps && obj["required"] is JsonArray reqs)
            {
                var validReqs = new JsonArray();
                foreach (var r in reqs)
                {
                    var rStr = r?.GetValue<string>();
                    if (rStr != null && validProps.ContainsKey(rStr))
                    {
                        validReqs.Add(rStr);
                    }
                }
                if (validReqs.Count > 0)
                    obj["required"] = validReqs;
                else
                    obj.Remove("required");
            }

            // 8. 处理 type (Lowercase + Nullable)
            if (obj.TryGetPropertyValue("type", out var typeNode))
            {
                string selectedType = "string";
                if (typeNode is JsonValue val && val.TryGetValue<string>(out var s))
                {
                    var lower = s.ToLowerInvariant();
                    if (lower == "null") isEffectivelyNullable = true;
                    else selectedType = lower;
                }
                else if (typeNode is JsonArray arr)
                {
                    foreach (var item in arr)
                    {
                        var t = item?.GetValue<string>()?.ToLowerInvariant();
                        if (t == "null") isEffectivelyNullable = true;
                        else if (selectedType == "string" && !string.IsNullOrEmpty(t)) selectedType = t;
                    }
                }
                obj["type"] = selectedType;
            }
            else
            {
                obj["type"] = obj.ContainsKey("properties") ? "object" : "string";
            }

            if (isEffectivelyNullable)
            {
                AppendDescription(obj, "(nullable)");
            }

            // 9. Enum 转字符串
            if (obj["enum"] is JsonArray enumArr)
            {
                for (int i = 0; i < enumArr.Count; i++)
                {
                    var item = enumArr[i];
                    if (item != null && item.GetValueKind() != JsonValueKind.String)
                    {
                        enumArr[i] = item.ToJsonString();
                    }
                }
            }
        }

        return isEffectivelyNullable;
    }

    private static void MergeAllOf(JsonObject obj)
    {
        if (obj["allOf"] is not JsonArray allOf) return;
        obj.Remove("allOf");

        foreach (var sub in allOf)
        {
            if (sub is not JsonObject subObj) continue;

            // Merge properties
            if (subObj["properties"] is JsonObject subProps)
            {
                if (obj["properties"] is not JsonObject targetProps)
                {
                    targetProps = new JsonObject();
                    obj["properties"] = targetProps;
                }
                foreach (var kv in subProps)
                {
                    targetProps[kv.Key] = kv.Value?.DeepClone();
                }
            }

            // Merge required
            if (subObj["required"] is JsonArray subReq)
            {
                if (obj["required"] is not JsonArray targetReq)
                {
                    targetReq = new JsonArray();
                    obj["required"] = targetReq;
                }
                var existing = targetReq.Select(x => x?.GetValue<string>()).ToHashSet();
                foreach (var r in subReq)
                {
                    var rStr = r?.GetValue<string>();
                    if (rStr != null && !existing.Contains(rStr))
                    {
                        targetReq.Add(rStr);
                        existing.Add(rStr);
                    }
                }
            }

            // Merge others
            foreach (var kv in subObj)
            {
                if (kv.Key != "properties" && kv.Key != "required" && kv.Key != "allOf" && !obj.ContainsKey(kv.Key))
                {
                    obj[kv.Key] = kv.Value?.DeepClone();
                }
            }
        }
    }

    private static void MigrateConstraints(JsonObject obj)
    {
        var hints = new List<string>();
        foreach (var c in ConstraintMap)
        {
            if (obj.TryGetPropertyValue(c.Key, out var val) && val != null)
            {
                hints.Add($"{c.Value}: {val}");
            }
        }

        if (hints.Count > 0)
        {
            AppendDescription(obj, $"[Constraint: {string.Join(", ", hints)}]");
        }
    }

    private static void AppendDescription(JsonObject obj, string text)
    {
        var desc = obj["description"]?.GetValue<string>() ?? "";
        if (!desc.Contains(text))
        {
            if (!string.IsNullOrEmpty(desc)) desc += " ";
            desc += text;
            obj["description"] = desc;
        }
    }

    private static JsonNode? ExtractBestSchemaFromUnion(JsonArray unionArray)
    {
        JsonNode? best = null;
        int bestScore = -1;

        foreach (var item in unionArray)
        {
            int score = ScoreSchemaOption(item);
            if (score > bestScore)
            {
                bestScore = score;
                best = item;
            }
        }
        return best?.DeepClone();
    }

    private static int ScoreSchemaOption(JsonNode? node)
    {
        if (node is not JsonObject obj) return 0;

        if (obj.ContainsKey("properties") || obj["type"]?.GetValue<string>() == "object") return 3;
        if (obj.ContainsKey("items") || obj["type"]?.GetValue<string>() == "array") return 2;
        var t = obj["type"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(t) && t != "null") return 1;

        return 0;
    }
}

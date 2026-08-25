using System.Text.Json;
using AiRelay.Application.ModelRoutes.Dtos;
using AiRelay.Domain.Shared.Json;
using Xunit;

namespace AiRelay.Tests.ModelRoutes;

/// <summary>
/// /v1/models 代理端点响应必须符合 OpenAI / Anthropic 线协议（snake_case 字段名）。
/// 端点经 MVC 管道以 JsonOptions.WebApi（camelCase + 忽略 null）序列化，
/// 协议字段必须通过 JsonPropertyName 显式固定，不受全局命名策略影响。
/// </summary>
public class ProxyModelsResponseFactoryTests
{
    private static string Serialize(ProxyModelsOutputDto dto) =>
        JsonSerializer.Serialize(dto, JsonOptions.WebApi);

    [Fact]
    public void OpenAiResponse_UsesProtocolFieldNames()
    {
        var json = Serialize(ProxyModelsResponseFactory.CreateOpenAi(["gpt-4o"]));

        Assert.Contains("\"object\":\"list\"", json);
        Assert.Contains("\"id\":\"gpt-4o\"", json);
        Assert.Contains("\"object\":\"model\"", json);
        Assert.Contains("\"created\":", json);
        Assert.Contains("\"owned_by\":\"system\"", json);
        Assert.DoesNotContain("ownedBy", json);
    }

    [Fact]
    public void AnthropicResponse_UsesProtocolFieldNames()
    {
        var json = Serialize(ProxyModelsResponseFactory.CreateAnthropic(["claude-x", "claude-y"]));

        Assert.Contains("\"type\":\"model\"", json);
        Assert.Contains("\"display_name\":\"claude-x\"", json);
        Assert.Contains("\"created_at\":", json);
        Assert.Contains("\"has_more\":false", json);
        Assert.Contains("\"first_id\":\"claude-x\"", json);
        Assert.Contains("\"last_id\":\"claude-y\"", json);
        Assert.DoesNotContain("displayName", json);
        // Anthropic 协议条目没有 object 字段
        Assert.DoesNotContain("\"object\"", json);
    }

    [Fact]
    public void AnthropicResponse_Empty_OmitsFirstAndLastIds()
    {
        var json = Serialize(ProxyModelsResponseFactory.CreateAnthropic([]));

        Assert.Contains("\"has_more\":false", json);
        Assert.DoesNotContain("first_id", json);
        Assert.DoesNotContain("last_id", json);
    }
}

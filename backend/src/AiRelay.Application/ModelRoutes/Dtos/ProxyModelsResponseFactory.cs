namespace AiRelay.Application.ModelRoutes.Dtos;

/// <summary>
/// /v1/models 代理端点响应工厂 — 按下游协议的线格式组装响应。
/// </summary>
public static class ProxyModelsResponseFactory
{
    public static ProxyModelsOutputDto CreateOpenAi(IEnumerable<string> modelIds)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new ProxyModelsOutputDto
        {
            Object = "list",
            Data = modelIds.Select(id => new ProxyModelItemDto
            {
                Id = id,
                Object = "model",
                Created = created,
                OwnedBy = "system"
            }).ToList()
        };
    }

    public static ProxyModelsOutputDto CreateAnthropic(IEnumerable<string> modelIds)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var data = modelIds.Select(id => new ProxyModelItemDto
        {
            Id = id,
            Type = "model",
            DisplayName = id,
            CreatedAt = createdAt
        }).ToList();

        return new ProxyModelsOutputDto
        {
            Data = data,
            HasMore = false,
            FirstId = data.Count > 0 ? data[0].Id : null,
            LastId = data.Count > 0 ? data[^1].Id : null
        };
    }
}

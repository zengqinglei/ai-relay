namespace AiRelay.Application.ApiKeys.Dtos;

public class EnableApiKeyInputDto
{
    /// <summary>
    /// 新的过期时间（可选，若提供则更新）
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// ApiKey 分组绑定输出 DTO
/// </summary>
public class ApiKeyBindingOutputDto
{
    public Guid Id { get; set; }
    public Guid ApiKeyId { get; set; }
    public ProviderPlatform Platform { get; set; }
    public Guid ProviderGroupId { get; set; }
    public string ProviderGroupName { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
}

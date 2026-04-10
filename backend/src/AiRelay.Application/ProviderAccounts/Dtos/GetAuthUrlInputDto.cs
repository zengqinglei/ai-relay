using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ProviderAccounts.Dtos;

public class GetAuthUrlInputDto
{
    public Provider Provider { get; set; }
}

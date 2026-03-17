namespace AiRelay.Application.ProviderAccounts.Dtos;

public class OAuthUrlOutputDto
{
    public required string AuthUrl { get; set; }
    public required string SessionId { get; set; }
}

using AiRelay.Application.ApiKeys.Dtos;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.Shared.Security.Aes;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ApiKeys.Mappings;

public class ApiKeyProfile : Profile
{
    public ApiKeyProfile()
    {
        CreateMap<ApiKey, ApiKeyOutputDto>()
            .ForMember(dest => dest.Secret, opt => opt.MapFrom<ApiKeySecretResolver>())
            .AfterMap<ApiKeyStatsMappingAction>();

        CreateMap<ApiKeyProviderGroupBinding, ApiKeyBindingOutputDto>()
            .ForMember(dest => dest.ProviderGroupName, opt => opt.MapFrom(src => src.ProviderGroup.Name));
    }
}

/// <summary>
/// API Key 密钥解密解析器
/// </summary>
public class ApiKeySecretResolver : IValueResolver<ApiKey, ApiKeyOutputDto, string>
{
    private readonly IAesEncryptionProvider _aesEncryptionProvider;
    private readonly ILogger<ApiKeySecretResolver> _logger;

    public ApiKeySecretResolver(
        IAesEncryptionProvider aesEncryptionProvider,
        ILogger<ApiKeySecretResolver> logger)
    {
        _aesEncryptionProvider = aesEncryptionProvider;
        _logger = logger;
    }

    public string Resolve(ApiKey source, ApiKeyOutputDto destination, string destMember, ResolutionContext context)
    {
        try
        {
            return _aesEncryptionProvider.Decrypt(source.EncryptedSecret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解密 API Key 失败: {Id}", source.Id);
            return "***DECRYPT_ERROR***";
        }
    }
}

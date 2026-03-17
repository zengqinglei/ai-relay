using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AutoMapper;

namespace AiRelay.Application.ProviderAccounts.Mappings;

/// <summary>
/// Model Option AutoMapper 配置
/// </summary>
public class ModelOptionProfile : Profile
{
    public ModelOptionProfile()
    {
        CreateMap<ModelOption, ModelOptionOutputDto>();
    }
}

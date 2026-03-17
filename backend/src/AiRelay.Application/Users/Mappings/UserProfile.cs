using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Users.Entities;
using AutoMapper;

namespace AiRelay.Application.Users.Mappings;

/// <summary>
/// 用户映射配置
/// </summary>
public class UserProfile : Profile
{
    public UserProfile()
    {
        // User -> UserOutputDto
        CreateMap<User, UserOutputDto>()
            .ForMember(dest => dest.Roles, opt => opt.MapFrom<UserRolesResolver>());
    }
}

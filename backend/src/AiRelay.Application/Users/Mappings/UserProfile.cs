using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Users.Entities;
using Leistd.ObjectMapping.Mapster;
using Mapster;

namespace AiRelay.Application.Users.Mappings;

/// <summary>
/// 用户映射配置
/// </summary>
public class UserProfile : MapsterProfile
{
    protected override void ConfigureMappings()
    {
        // User -> UserOutputDto
        CreateMap<User, UserOutputDto>()
            .Map(dest => dest.Roles, src => ResolveRoles(src));
    }

    private static string[] ResolveRoles(User source)
    {
        if (MapContext.Current?.Parameters.TryGetValue("UserRoles", out var userRolesObj) == true &&
            userRolesObj is List<UserRole> userRoles &&
            MapContext.Current?.Parameters.TryGetValue("Roles", out var rolesObj) == true &&
            rolesObj is List<Role> roles)
        {
            return userRoles
                .Where(ur => ur.UserId == source.Id)
                .Join(roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .ToArray();
        }

        return [];
    }
}

using AiRelay.Application.Auth.Dtos;
using AiRelay.Domain.Users.Entities;
using AutoMapper;

namespace AiRelay.Application.Users.Mappings;

public class UserRolesResolver : IValueResolver<User, UserOutputDto, string[]>
{
    public string[] Resolve(User source, UserOutputDto destination, string[] destMember, ResolutionContext context)
    {
        // 从上下文获取预取的数据（现在所有调用都保证传递上下文）
        if (context.Items.TryGetValue("UserRoles", out var userRolesObj) &&
            userRolesObj is List<UserRole> userRoles &&
            context.Items.TryGetValue("Roles", out var rolesObj) &&
            rolesObj is List<Role> roles)
        {
            return userRoles
                .Where(ur => ur.UserId == source.Id)
                .Join(roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .ToArray();
        }

        // 如果上下文数据缺失，返回空数组
        return [];
    }
}

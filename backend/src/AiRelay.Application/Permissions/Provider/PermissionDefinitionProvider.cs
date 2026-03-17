using Leistd.Ddd.Application.Permission;

namespace AiRelay.Application.Permissions.Provider;

/// <summary>
/// 权限定义提供器
/// </summary>
public class PermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void Define(IPermissionDefinitionContext context)
    {
        var aiRelayGroup = context.GetOrAddGroup(
            PermissionConstant.GroupName,
            displayName: "系统权限"
        );

        // 用户管理
        var usersPermission = aiRelayGroup.AddPermission(
            PermissionConstant.Users.Default,
            displayName: "用户管理"
        );
        usersPermission.AddChild(PermissionConstant.Users.Create, displayName: "创建用户");
        usersPermission.AddChild(PermissionConstant.Users.Update, displayName: "更新用户");
        usersPermission.AddChild(PermissionConstant.Users.Delete, displayName: "删除用户");
        usersPermission.AddChild(PermissionConstant.Users.ManageRoles, displayName: "管理用户角色");

        // 角色管理
        var rolesPermission = aiRelayGroup.AddPermission(
            PermissionConstant.Roles.Default,
            displayName: "角色管理"
        );
        rolesPermission.AddChild(PermissionConstant.Roles.Create, displayName: "创建角色");
        rolesPermission.AddChild(PermissionConstant.Roles.Update, displayName: "更新角色");
        rolesPermission.AddChild(PermissionConstant.Roles.Delete, displayName: "删除角色");
        rolesPermission.AddChild(PermissionConstant.Roles.ManagePermissions, displayName: "管理角色权限");

        // API Key 管理
        var apiKeysPermission = aiRelayGroup.AddPermission(
            PermissionConstant.ApiKeys.Default,
            displayName: "API Key 管理"
        );
        apiKeysPermission.AddChild(PermissionConstant.ApiKeys.Create, displayName: "创建 API Key");
        apiKeysPermission.AddChild(PermissionConstant.ApiKeys.Update, displayName: "更新 API Key");
        apiKeysPermission.AddChild(PermissionConstant.ApiKeys.Delete, displayName: "删除 API Key");
        apiKeysPermission.AddChild(PermissionConstant.ApiKeys.View, displayName: "查看 API Key");

        // API 代理
        var apiProxyPermission = aiRelayGroup.AddPermission(
            PermissionConstant.ApiProxy.Default,
            displayName: "API 代理访问"
        );
        apiProxyPermission.AddChild(PermissionConstant.ApiProxy.Claude, displayName: "Claude API");
        apiProxyPermission.AddChild(PermissionConstant.ApiProxy.Gemini, displayName: "Gemini API");
        apiProxyPermission.AddChild(PermissionConstant.ApiProxy.OpenAI, displayName: "OpenAI API");

        // 提供商账号管理
        var providerAccountsPermission = aiRelayGroup.AddPermission(
            PermissionConstant.ProviderAccounts.Default,
            displayName: "提供商账号管理"
        );
        providerAccountsPermission.AddChild(PermissionConstant.ProviderAccounts.Create, displayName: "创建账号");
        providerAccountsPermission.AddChild(PermissionConstant.ProviderAccounts.Update, displayName: "更新账号");
        providerAccountsPermission.AddChild(PermissionConstant.ProviderAccounts.Delete, displayName: "删除账号");
        providerAccountsPermission.AddChild(PermissionConstant.ProviderAccounts.View, displayName: "查看账号");

        // 系统设置
        var settingsPermission = aiRelayGroup.AddPermission(
            PermissionConstant.Settings.Default,
            displayName: "系统设置"
        );
        settingsPermission.AddChild(PermissionConstant.Settings.Update, displayName: "更新设置");

        // 审计日志
        var auditLogsPermission = aiRelayGroup.AddPermission(
            PermissionConstant.AuditLogs.Default,
            displayName: "审计日志"
        );
        auditLogsPermission.AddChild(PermissionConstant.AuditLogs.View, displayName: "查看日志");
    }
}

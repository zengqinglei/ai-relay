namespace AiRelay.Application.Permissions.Provider;

/// <summary>
/// 权限定义
/// </summary>
public static class PermissionConstant
{
    /// <summary>
    /// 权限组前缀
    /// </summary>
    public const string GroupName = "AiRelay";

    /// <summary>
    /// 用户管理权限
    /// </summary>
    public static class Users
    {
        public const string Default = GroupName + ".Users";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
        public const string ManageRoles = Default + ".ManageRoles";
    }

    /// <summary>
    /// 角色管理权限
    /// </summary>
    public static class Roles
    {
        public const string Default = GroupName + ".Roles";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
        public const string ManagePermissions = Default + ".ManagePermissions";
    }

    /// <summary>
    /// API Key 管理权限
    /// </summary>
    public static class ApiKeys
    {
        public const string Default = GroupName + ".ApiKeys";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
        public const string View = Default + ".View";
    }

    /// <summary>
    /// API 代理访问权限
    /// </summary>
    public static class ApiProxy
    {
        public const string Default = GroupName + ".ApiProxy";
        public const string Claude = Default + ".Claude";
        public const string Gemini = Default + ".Gemini";
        public const string OpenAI = Default + ".OpenAI";
    }

    /// <summary>
    /// 提供商账号管理权限
    /// </summary>
    public static class ProviderAccounts
    {
        public const string Default = GroupName + ".ProviderAccounts";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
        public const string View = Default + ".View";
    }

    /// <summary>
    /// 系统设置权限
    /// </summary>
    public static class Settings
    {
        public const string Default = GroupName + ".Settings";
        public const string Update = Default + ".Update";
    }

    /// <summary>
    /// 审计日志权限
    /// </summary>
    public static class AuditLogs
    {
        public const string Default = GroupName + ".AuditLogs";
        public const string View = Default + ".View";
    }
}

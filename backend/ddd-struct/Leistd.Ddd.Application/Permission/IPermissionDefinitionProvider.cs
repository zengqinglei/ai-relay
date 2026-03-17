namespace Leistd.Ddd.Application.Permission;

/// <summary>
/// 权限定义提供者
/// </summary>
/// <remarks>
/// 业务项目可实现此接口来定义权限
/// 示例：AiRelayPermissionDefinitionProvider
/// </remarks>
public interface IPermissionDefinitionProvider
{
    /// <summary>
    /// 定义权限
    /// </summary>
    /// <param name="context">权限定义上下文</param>
    void Define(IPermissionDefinitionContext context);
}

/// <summary>
/// 权限定义上下文
/// </summary>
public interface IPermissionDefinitionContext
{
    /// <summary>
    /// 获取或创建权限组
    /// </summary>
    /// <param name="name">组名称</param>
    /// <param name="displayName">显示名称</param>
    /// <returns>权限组</returns>
    IPermissionGroupDefinition GetOrAddGroup(string name, string? displayName = null);

    /// <summary>
    /// 添加权限
    /// </summary>
    /// <param name="name">权限名称</param>
    /// <param name="displayName">显示名称</param>
    /// <returns>权限定义</returns>
    IPermissionDefinition AddPermission(string name, string? displayName = null);

    /// <summary>
    /// 获取权限定义
    /// </summary>
    /// <param name="name">权限名称</param>
    /// <returns>权限定义，如果不存在则返回 null</returns>
    IPermissionDefinition? GetPermissionOrNull(string name);
}

/// <summary>
/// 权限组定义
/// </summary>
public interface IPermissionGroupDefinition
{
    /// <summary>
    /// 组名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 显示名称
    /// </summary>
    string? DisplayName { get; set; }

    /// <summary>
    /// 添加权限到组
    /// </summary>
    /// <param name="name">权限名称</param>
    /// <param name="displayName">显示名称</param>
    /// <returns>权限定义</returns>
    IPermissionDefinition AddPermission(string name, string? displayName = null);

    /// <summary>
    /// 获取组内的权限定义
    /// </summary>
    /// <param name="name">权限名称</param>
    /// <returns>权限定义，如果不存在则返回 null</returns>
    IPermissionDefinition? GetPermissionOrNull(string name);
}

/// <summary>
/// 权限定义
/// </summary>
public interface IPermissionDefinition
{
    /// <summary>
    /// 权限名称（唯一标识）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 显示名称
    /// </summary>
    string? DisplayName { get; set; }

    /// <summary>
    /// 父权限
    /// </summary>
    IPermissionDefinition? Parent { get; }

    /// <summary>
    /// 子权限集合
    /// </summary>
    IReadOnlyList<IPermissionDefinition> Children { get; }

    /// <summary>
    /// 是否启用
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// 添加子权限
    /// </summary>
    /// <param name="name">权限名称</param>
    /// <param name="displayName">显示名称</param>
    /// <returns>子权限定义</returns>
    IPermissionDefinition AddChild(string name, string? displayName = null);
}

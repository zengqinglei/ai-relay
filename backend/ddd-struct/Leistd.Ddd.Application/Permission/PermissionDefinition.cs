namespace Leistd.Ddd.Application.Permission;

/// <summary>
/// 权限定义
/// </summary>
internal class PermissionDefinition : IPermissionDefinition
{
    private readonly List<PermissionDefinition> _children = new();

    public string Name { get; }
    public string? DisplayName { get; set; }
    public IPermissionDefinition? Parent { get; private set; }
    public IReadOnlyList<IPermissionDefinition> Children => _children;
    public bool IsEnabled { get; set; } = true;

    public PermissionDefinition(string name, string? displayName = null, IPermissionDefinition? parent = null)
    {
        Name = name;
        DisplayName = displayName;
        Parent = parent;
    }

    public IPermissionDefinition AddChild(string name, string? displayName = null)
    {
        var child = new PermissionDefinition(name, displayName, this);
        _children.Add(child);
        return child;
    }
}

/// <summary>
/// 权限组定义
/// </summary>
internal class PermissionGroupDefinition : IPermissionGroupDefinition
{
    private readonly Dictionary<string, PermissionDefinition> _permissions = new();

    public string Name { get; }
    public string? DisplayName { get; set; }

    public PermissionGroupDefinition(string name, string? displayName = null)
    {
        Name = name;
        DisplayName = displayName;
    }

    public IPermissionDefinition AddPermission(string name, string? displayName = null)
    {
        if (_permissions.ContainsKey(name))
            throw new InvalidOperationException($"权限 '{name}' 已存在于组 '{Name}' 中");

        var permission = new PermissionDefinition(name, displayName);
        _permissions[name] = permission;
        return permission;
    }

    public IPermissionDefinition? GetPermissionOrNull(string name)
    {
        return _permissions.TryGetValue(name, out var permission) ? permission : null;
    }

    public IEnumerable<IPermissionDefinition> GetAllPermissions()
    {
        return _permissions.Values;
    }
}

/// <summary>
/// 权限定义上下文
/// </summary>
internal class PermissionDefinitionContext : IPermissionDefinitionContext
{
    private readonly Dictionary<string, PermissionGroupDefinition> _groups = new();
    private readonly Dictionary<string, IPermissionDefinition> _permissionCache = new();

    public IPermissionGroupDefinition GetOrAddGroup(string name, string? displayName = null)
    {
        if (!_groups.TryGetValue(name, out var group))
        {
            group = new PermissionGroupDefinition(name, displayName);
            _groups[name] = group;
        }
        else if (displayName != null && group.DisplayName != displayName)
        {
            group.DisplayName = displayName;
        }

        return group;
    }

    public IPermissionDefinition AddPermission(string name, string? displayName = null)
    {
        if (_permissionCache.ContainsKey(name))
            throw new InvalidOperationException($"权限 '{name}' 已存在");

        var permission = new PermissionDefinition(name, displayName);
        _permissionCache[name] = permission;
        return permission;
    }

    public IPermissionDefinition? GetPermissionOrNull(string name)
    {
        // 先从缓存查找
        if (_permissionCache.TryGetValue(name, out var permission))
            return permission;

        // 从所有组中查找
        foreach (var group in _groups.Values)
        {
            permission = group.GetPermissionOrNull(name);
            if (permission != null)
            {
                _permissionCache[name] = permission;
                return permission;
            }

            // 递归查找子权限
            permission = FindPermissionRecursively(group.GetAllPermissions(), name);
            if (permission != null)
            {
                _permissionCache[name] = permission;
                return permission;
            }
        }

        return null;
    }

    private IPermissionDefinition? FindPermissionRecursively(IEnumerable<IPermissionDefinition> permissions, string name)
    {
        foreach (var permission in permissions)
        {
            if (permission.Name == name)
                return permission;

            var child = FindPermissionRecursively(permission.Children, name);
            if (child != null)
                return child;
        }

        return null;
    }

    public IEnumerable<IPermissionGroupDefinition> GetGroups()
    {
        return _groups.Values;
    }

    public IEnumerable<IPermissionDefinition> GetAllPermissions()
    {
        foreach (var group in _groups.Values)
        {
            foreach (var permission in GetAllPermissionsRecursively(group.GetAllPermissions()))
            {
                yield return permission;
            }
        }
    }

    private IEnumerable<IPermissionDefinition> GetAllPermissionsRecursively(IEnumerable<IPermissionDefinition> permissions)
    {
        foreach (var permission in permissions)
        {
            yield return permission;

            foreach (var child in GetAllPermissionsRecursively(permission.Children))
            {
                yield return child;
            }
        }
    }
}

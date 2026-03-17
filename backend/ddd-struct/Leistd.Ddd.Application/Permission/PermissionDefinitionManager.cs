using Microsoft.Extensions.Logging;

namespace Leistd.Ddd.Application.Permission;

/// <summary>
/// 权限定义管理器接口
/// </summary>
public interface IPermissionDefinitionManager
{
    /// <summary>
    /// 获取权限定义
    /// </summary>
    IPermissionDefinition? GetOrNull(string name);

    /// <summary>
    /// 获取所有权限定义
    /// </summary>
    IEnumerable<IPermissionDefinition> GetAll();
}

/// <summary>
/// 权限定义管理器
/// 负责加载和管理所有权限定义
/// </summary>
public class PermissionDefinitionManager : IPermissionDefinitionManager
{
    private readonly PermissionDefinitionContext _context;
    private readonly ILogger<PermissionDefinitionManager> _logger;

    public PermissionDefinitionManager(
        IEnumerable<IPermissionDefinitionProvider> providers,
        ILogger<PermissionDefinitionManager> logger)
    {
        _context = new PermissionDefinitionContext();
        _logger = logger;

        LoadPermissionDefinitions(providers);
    }

    private void LoadPermissionDefinitions(IEnumerable<IPermissionDefinitionProvider> providers)
    {
        _logger.LogInformation("开始加载权限定义...");

        var providerList = providers.ToList();
        _logger.LogInformation("找到 {Count} 个权限定义提供器", providerList.Count);

        foreach (var provider in providerList)
        {
            var providerType = provider.GetType().Name;
            try
            {
                _logger.LogDebug("加载权限定义提供器: {Provider}", providerType);
                provider.Define(_context);
                _logger.LogDebug("权限定义提供器 {Provider} 加载成功", providerType);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "加载权限定义提供器 {Provider} 失败", providerType);
                throw;
            }
        }

        var allPermissions = _context.GetAllPermissions().ToList();
        _logger.LogInformation("权限定义加载完成，共 {Count} 个权限", allPermissions.Count);
    }

    public IPermissionDefinition? GetOrNull(string name)
    {
        return _context.GetPermissionOrNull(name);
    }

    public IEnumerable<IPermissionDefinition> GetAll()
    {
        return _context.GetAllPermissions();
    }
}

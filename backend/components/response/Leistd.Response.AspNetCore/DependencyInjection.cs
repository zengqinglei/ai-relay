using Leistd.Response.AspNetCore.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Leistd.Response.AspNetCore;

public static class DependencyInjection
{
    /// <summary>
    /// 添加统一响应包装
    /// </summary>
    public static IServiceCollection AddResponseWrapper(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            options.Filters.Add<ResultWrapperFilter>();
        });

        return services;
    }
}

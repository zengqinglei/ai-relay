using System.Reflection;
using Leistd.ObjectMapping.Core;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leistd.ObjectMapping.Mapster;

public static class DependencyInjection
{
    /// <summary>
    /// 添加 Mapster 对象映射器
    /// </summary>
    public static IServiceCollection AddMapsterObjectMapper(
        this IServiceCollection services,
        Action<MapsterOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IMapper>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MapsterOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<MapsterObjectMapper>>();

            var config = new TypeAdapterConfig();
            config.Default.PreserveReference(true);

            foreach (var configurator in options.Configurators)
            {
                configurator(config);
            }

            if (options.ValidateMappings)
            {
                logger.LogInformation("验证 Mapster 映射配置");
                config.Compile();
            }

            return new Mapper(config);
        });

        services.AddSingleton<IObjectMapper, MapsterObjectMapper>();

        return services;
    }

    /// <summary>
    /// 从程序集中扫描并添加 MapsterProfile
    /// </summary>
    public static MapsterOptions AddProfiles(this MapsterOptions options, params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var profileTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(MapsterProfile)));

            foreach (var profileType in profileTypes)
            {
                options.Configurators.Add(config =>
                {
                    var profile = (MapsterProfile)Activator.CreateInstance(profileType)!;
                    profile.Configure(config);
                });
            }
        }

        return options;
    }
}

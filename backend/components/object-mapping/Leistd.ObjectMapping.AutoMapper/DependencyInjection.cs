using AutoMapper;
using Leistd.ObjectMapping.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leistd.ObjectMapping.AutoMapper;

public static class DependencyInjection
{
    /// <summary>
    /// 添加 AutoMapper 对象映射器
    /// </summary>
    public static IServiceCollection AddAutoMapperObjectMapper(
        this IServiceCollection services,
        Action<AutoMapperOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IMapper>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AutoMapperOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<AutoMapperObjectMapper>>();

            var config = new MapperConfiguration(cfg =>
            {
                cfg.ConstructServicesUsing(sp.GetService);

                foreach (var configurator in options.Configurators)
                {
                    configurator(cfg);
                }
            });

            if (options.ValidateMappings)
            {
                logger.LogInformation("验证 AutoMapper 映射配置");
                config.AssertConfigurationIsValid();
            }

            return config.CreateMapper();
        });

        services.AddSingleton<IObjectMapper, AutoMapperObjectMapper>();

        return services;
    }
}

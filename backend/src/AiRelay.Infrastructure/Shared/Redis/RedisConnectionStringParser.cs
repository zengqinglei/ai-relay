using StackExchange.Redis;

namespace AiRelay.Infrastructure.Shared.Redis;

/// <summary>
/// Redis 连接字符串解析工具
/// 支持标准格式和 Upstash rediss:// URL 格式
/// </summary>
public static class RedisConnectionStringParser
{
    /// <summary>
    /// 解析 Redis 连接字符串，支持 redis:// 和 rediss:// URL 格式
    /// </summary>
    public static ConfigurationOptions Parse(string connectionString)
    {
        if (connectionString.StartsWith("redis://") || connectionString.StartsWith("rediss://"))
        {
            var uri = new Uri(connectionString);
            var config = new ConfigurationOptions
            {
                EndPoints = { { uri.Host, uri.Port } },
                Ssl = uri.Scheme == "rediss",
                AbortOnConnectFail = false,
                ConnectTimeout = 10000,
                SyncTimeout = 10000,
                KeepAlive = 60
            };

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':');
                if (parts.Length == 2)
                {
                    config.User = parts[0];
                    config.Password = parts[1];
                }
                else if (parts.Length == 1)
                {
                    config.Password = parts[0];
                }
            }

            return config;
        }

        return ConfigurationOptions.Parse(connectionString);
    }
}

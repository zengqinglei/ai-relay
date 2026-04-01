using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.UsageRecords.Options;
using Leistd.Exception.Core;
using Microsoft.Extensions.Options;

namespace AiRelay.Api.Middleware.SmartProxy.RequestProcessor;

public class DownstreamRequestProcessor(IOptions<UsageLoggingOptions> loggingOptions) : IDownstreamRequestProcessor
{
    private readonly UsageLoggingOptions _loggingOptions = loggingOptions.Value;

    private const long MaxBodySize = 100 * 1024 * 1024; // 100MB

    public async Task<DownRequestContext> ProcessAsync(
        HttpContext context,
        IChatModelHandler chatModelHandler,
        Guid apiKeyId,
        CancellationToken cancellationToken = default)
    {
        var request = context.Request;
        var contentType = request.ContentType ?? "";
        var isMultipart = contentType.Contains("multipart", StringComparison.OrdinalIgnoreCase);

        ReadOnlyMemory<byte> bodyBytes = ReadOnlyMemory<byte>.Empty;

        if (request.ContentLength > 0 && !isMultipart)
        {
            // 检查大小限制
            if (request.ContentLength > MaxBodySize)
            {
                throw new BadRequestException(
                    $"Request body too large, limit is {MaxBodySize / (1024 * 1024)}MB");
            }

            // 读取为字节数组（只读取一次）
            request.EnableBuffering();

            using var ms = new MemoryStream((int)request.ContentLength.Value);
            await request.Body.CopyToAsync(ms, cancellationToken);
            bodyBytes = ms.ToArray();

            // 重置位置（允许其他中间件访问）
            request.Body.Position = 0;
        }

        // 从 RouteValues 获取实际的 API 路径（去除本地路由前缀）
        var relativePath = "";
        if (context.Request.RouteValues.TryGetValue("catch-all", out var catchAll) && catchAll != null)
        {
            relativePath = "/" + catchAll.ToString()!.TrimStart('/');
        }

        var downContext = new DownRequestContext
        {
            Method = ParseHttpMethod(request.Method),
            RelativePath = relativePath,
            QueryString = request.QueryString.Value,
            Headers = ConvertHeaders(request.Headers),
            BodyBytes = bodyBytes,
            IsMultipart = isMultipart
        };

        // 各平台 Handler 负责从 Header/Body 提取元信息（ModelId、SessionHash）
        chatModelHandler.ExtractModelInfo(downContext, apiKeyId);

        return downContext;
    }

    private static HttpMethod ParseHttpMethod(string method)
    {
        return method?.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => HttpMethod.Post
        };
    }

    private static Dictionary<string, string> ConvertHeaders(IHeaderDictionary headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            result[header.Key] = header.Value.ToString();
        }
        return result;
    }
}

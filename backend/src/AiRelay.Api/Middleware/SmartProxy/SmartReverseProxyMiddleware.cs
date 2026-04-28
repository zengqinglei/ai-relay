using AiRelay.Api.Authentication;
using AiRelay.Api.Middleware.SmartProxy.ErrorHandling;
using AiRelay.Api.Middleware.SmartProxy.Handlers;
using AiRelay.Application.ModelRoutes;
using AiRelay.Application.ModelRoutes.Dtos;
using AiRelay.Application.ModelRoutes.Handlers;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Helpers;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Domain.UsageRecords.ValueObjects;
using Leistd.Exception.Core;
using Leistd.Tracing.Core.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

namespace AiRelay.Api.Middleware.SmartProxy;

public class SmartReverseProxyMiddleware(
    IModelRouteAppService modelRouteAppService,
    IChatModelHandlerFactory chatModelHandlerFactory,
    ProxyErrorFormatterFactory errorFormatterFactory,
    IOptions<UsageLoggingOptions> loggingOptions,
    ICorrelationIdProvider correlationIdProvider)
{
    private readonly UsageLoggingOptions _loggingOptions = loggingOptions.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var (routeProfile, apiKeyId, apiKeyName, userId) = ValidateAndGetContext(context);
        var correlationId = correlationIdProvider.Get() ?? correlationIdProvider.Create();

        var chatModelHandler = chatModelHandlerFactory.CreateHandler(routeProfile);
        var downContext = await ProcessDownstreamRequestAsync(context, routeProfile, chatModelHandler, apiKeyId);
        
        var metadata = new RouteExecutionMetadata(
            UsageRecordId: Guid.CreateVersion7(),
            UserId: userId,
            Source: UsageSource.ApiProxy,
            CorrelationId: correlationId,
            ApiKeyId: apiKeyId,
            ApiKeyName: apiKeyName);

        var responseHandler = new ProxyRouteResponseHandler(context, errorFormatterFactory, routeProfile);

        var candidateGroups = await modelRouteAppService.ResolveProxyRouteCandidatesAsync(new SelectProxyAccountInputDto
        {
            ApiKeyId = apiKeyId,
            ApiKeyName = apiKeyName,
            SessionHash = downContext.SessionId,
            ModelId = downContext.ModelId,
            AllowedCombinations = RouteProfileRegistry.Profiles.TryGetValue(routeProfile, out var profileDef)
                ? profileDef.SupportedCombinations
                : null
        }, context.RequestAborted);

        Func<SelectAccountResultDto, DownRequestContext> downContextModifier = _ =>
        {
            // 对于代理请求，downContext 是固定的，因为它是从原始 HTTP 请求中解析出来的
            // 流的位置（如果读取过的话）由内部的 Stream 处理，或者在这里确保其可重复读取
            if (downContext.RawStream != null && downContext.RawStream.CanSeek)
            {
                downContext.RawStream.Position = 0;
            }
            return downContext;
        };

        await modelRouteAppService.ExecuteRouteAsync(downContext, metadata, candidateGroups, downContextModifier, responseHandler, context.RequestAborted);
    }

    private async Task<DownRequestContext> ProcessDownstreamRequestAsync(
        HttpContext context, RouteProfile routeProfile, IChatModelHandler chatModelHandler, Guid apiKeyId)
    {
        var request = context.Request;
        var contentType = request.ContentType ?? "";
        var isMultipart = contentType.Contains("multipart", StringComparison.OrdinalIgnoreCase);

        const long MaxBodySize = 100 * 1024 * 1024; // 100MB
        bool hasBody = request.ContentLength > 0 || request.Headers.ContainsKey("Transfer-Encoding");
        if (hasBody && !isMultipart)
        {
            if (request.ContentLength > MaxBodySize)
                throw new BadRequestException($"Request body too large, limit is {MaxBodySize / (1024 * 1024)}MB");

            request.EnableBuffering(MaxBodySize);
        }

        var pathPrefix = RouteProfileRegistry.Profiles.TryGetValue(routeProfile, out var profileDef)
            ? profileDef.PathPrefix
            : string.Empty;

        var relativePath = pathPrefix;
        if (context.Request.RouteValues.TryGetValue("catch-all", out var catchAll) && catchAll != null)
        {
            var catchAllPath = catchAll.ToString()!;
            var separator = pathPrefix.Contains(':') ? ":" : "/";
            relativePath = string.IsNullOrEmpty(catchAllPath)
                ? pathPrefix
                : $"{pathPrefix}{separator}{catchAllPath}";
        }

        var rawStream = (hasBody && !isMultipart) ? request.Body : null;
        var (extractedProps, bodyPreview) = await JsonExtractHelper.ExtractEssentialPropsAsync(
            rawStream, _loggingOptions.IsBodyLoggingEnabled, _loggingOptions.MaxBodyLength);

        var downContext = new DownRequestContext
        {
            Method = ParseHttpMethod(request.Method),
            RelativePath = relativePath,
            QueryString = request.QueryString.Value,
            Headers = ConvertHeaders(request.Headers),
            RawStream = rawStream,
            IsMultipart = isMultipart,
            ExtractedProps = extractedProps,
            PreloadedBodyPreview = bodyPreview,
            DownRequestUrl = context.Request.GetDisplayUrl(),
            ClientIp = context.Connection.RemoteIpAddress?.ToString()
        };

        chatModelHandler.ExtractModelInfo(downContext, apiKeyId);
        return downContext;
    }

    private static HttpMethod ParseHttpMethod(string method) => method?.ToUpperInvariant() switch
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

    private static Dictionary<string, string> ConvertHeaders(IHeaderDictionary headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers) result[header.Key] = header.Value.ToString();
        return result;
    }

    private static (RouteProfile Profile, Guid ApiKeyId, string ApiKeyName, Guid UserId) ValidateAndGetContext(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<PlatformMetadata>();
        if (metadata == null) throw new NotFoundException("平台路由未配置");

        var apiKeyIdClaim = context.User.FindFirst(AuthenticationConstants.ApiKeyIdClaimType);
        var apiKeyNameClaim = context.User.FindFirst(AuthenticationConstants.ApiKeyNameClaimType);
        var userIdClaim = context.User.FindFirst(AuthenticationConstants.UserIdClaimType);

        if (apiKeyIdClaim == null || !Guid.TryParse(apiKeyIdClaim.Value, out var apiKeyId) ||
            apiKeyNameClaim == null ||
            userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedException("请求未经认证");
        }

        return (metadata.Profile, apiKeyId, apiKeyNameClaim.Value, userId);
    }
}

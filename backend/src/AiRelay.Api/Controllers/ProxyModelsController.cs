using AiRelay.Api.Authentication;
using AiRelay.Application.ModelRoutes;
using AiRelay.Application.ModelRoutes.Dtos;
using Leistd.Exception.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 代理协议模型列表端点 — 供 Claude Code CLI / OpenAI SDK 等下游客户端调用。
/// 路由 /v1/models 由 MVC 控制器优先处理，不进入 SmartReverseProxyMiddleware。
/// </summary>
[Route("v1/models")]
[Authorize(Policy = AuthorizationPolicies.AiProxyPolicy)]
public class ProxyModelsController(IModelRouteAppService modelRouteAppService) : BaseController
{
    /// <summary>
    /// 获取当前 ApiKey 可用的模型列表（聚合所有绑定分组，仅读缓存，无网络延迟）
    /// </summary>
    [HttpGet]
    public async Task<ProxyModelsOutputDto> GetAsync(CancellationToken cancellationToken)
    {
        var apiKeyIdClaim = User.FindFirst(AuthenticationConstants.ApiKeyIdClaimType);
        if (apiKeyIdClaim == null || !Guid.TryParse(apiKeyIdClaim.Value, out var apiKeyId))
        {
            throw new UnauthorizedException("请求未经认证");
        }

        var format = DetectResponseFormat(Request);
        var result = await modelRouteAppService.GetProxyModelsAsync(apiKeyId, format, cancellationToken);
        return result;
    }

    /// <summary>
    /// 根据请求头嗅探下游客户端协议类型：
    ///   anthropic-version → Anthropic 格式（优先）
    ///   x-api-key         → Anthropic 格式（老版 SDK 兼容）
    ///   x-goog-api-key    → Gemini 格式
    ///   默认              → OpenAI 格式
    /// </summary>
    private static string DetectResponseFormat(HttpRequest request)
    {
        if (request.Headers.ContainsKey("anthropic-version")) return "anthropic";
        if (request.Headers.ContainsKey("x-api-key"))         return "anthropic";
        if (request.Headers.ContainsKey("x-goog-api-key"))    return "gemini";
        return "openai";
    }
}

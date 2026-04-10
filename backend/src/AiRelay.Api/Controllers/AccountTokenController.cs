using System.Text;
using System.Text.Json;
using AiRelay.Application.ProviderAccounts.AppServices;
using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using Leistd.Ddd.Application.Contracts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 账户令牌管理控制器
/// </summary>
[Authorize]
[Route("api/v1/account-tokens")]
public class AccountTokenController(IAccountTokenAppService accountTokenAppService) : BaseController
{
    /// <summary>
    /// 模型调试测试 (SSE 流式响应)
    /// </summary>
    [HttpPost("{id}/model-test")]
    public async Task DebugModelAsync(
        Guid id,
        [FromBody] ChatMessageInputDto input,
        CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var options = Domain.Shared.Json.JsonOptions.WebApi;

        try
        {
            await foreach (var evt in accountTokenAppService.DebugModelAsync(id, input, cancellationToken))
            {
                var data = JsonSerializer.Serialize(evt, options);
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {data}\n\n"), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var errorEvt = new StreamEvent { Type = StreamEventType.Error, Content = $"连接意外中断: {ex.Message}" };
            var data = JsonSerializer.Serialize(errorEvt, options);
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {data}\n\n"), CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }

        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes("data: [DONE]\n\n"), cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// 获取指定提供商的可用模型列表
    /// </summary>
    /// <param name="provider">提供商类型</param>
    /// <param name="accountId">可选：账户ID（提供时尝试上游拉取）</param>
    [HttpGet("provider/{provider}/models")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableModels(
        Provider provider,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        var models = await accountTokenAppService.GetAvailableModelsAsync(provider, accountId, cancellationToken);
        return Ok(models);
    }

    [HttpGet("oauth-url")]
    public Task<OAuthUrlOutputDto> GetAuthUrl([FromQuery] GetAuthUrlInputDto input)
    {
        return accountTokenAppService.GetOAuthUrlAsync(input, HttpContext.RequestAborted);
    }

    /// <summary>
    /// 获取账户列表
    /// </summary>
    [HttpGet]
    public async Task<PagedResultDto<AccountTokenOutputDto>> GetPagedListAsync(
        [FromQuery] GetAccountTokenPagedInputDto input,
        CancellationToken cancellationToken)
    {
        return await accountTokenAppService.GetPagedListAsync(input, cancellationToken);
    }

    /// <summary>
    /// 获取账户详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<AccountTokenOutputDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await accountTokenAppService.GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// 创建账户
    /// </summary>
    [HttpPost]
    public async Task<AccountTokenOutputDto> CreateAsync(
        [FromBody] CreateAccountTokenInputDto input,
        CancellationToken cancellationToken)
    {
        return await accountTokenAppService.CreateAsync(input, cancellationToken);
    }

    /// <summary>
    /// 更新账户
    /// </summary>
    [HttpPut("{id}")]
    public async Task<AccountTokenOutputDto> UpdateAsync(
        Guid id,
        [FromBody] UpdateAccountTokenInputDto input,
        CancellationToken cancellationToken)
    {
        return await accountTokenAppService.UpdateAsync(id, input, cancellationToken);
    }

    /// <summary>
    /// 删除账户
    /// </summary>
    [HttpDelete("{id}")]
    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await accountTokenAppService.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// 启用账户
    /// </summary>
    [HttpPatch("{id}/enable")]
    public async Task EnableAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await accountTokenAppService.EnableAsync(id, cancellationToken);
    }

    /// <summary>
    /// 禁用账户
    /// </summary>
    [HttpPatch("{id}/disable")]
    public async Task DisableAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await accountTokenAppService.DisableAsync(id, cancellationToken);
    }

    /// <summary>
    /// 重置账户状态（限流/异常）
    /// </summary>
    [HttpPost("{id}/reset-status")]
    public async Task ResetStatusAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await accountTokenAppService.ResetStatusAsync(id, cancellationToken);
    }
}
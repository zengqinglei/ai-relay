using System.Text;
using System.Text.Json;
using AiRelay.Application.ChatSessions.AppServices;
using AiRelay.Application.ChatSessions.Dtos;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using Leistd.Ddd.Application.Contracts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 工作区聊天会话控制器
/// </summary>
[Authorize]
[Route("api/v1/chat-sessions")]
public class ChatSessionController(
    IChatSessionAppService chatSessionAppService,
    ILogger<ChatSessionController> logger) : BaseController
{
    [HttpGet]
    public Task<List<ChatSessionOutputDto>> GetListAsync(CancellationToken cancellationToken)
        => chatSessionAppService.GetListAsync(cancellationToken);

    [HttpGet("model-options")]
    public Task<IReadOnlyList<ChatModelOptionOutputDto>> GetModelOptionsAsync(
        [FromQuery] Guid? providerGroupId,
        CancellationToken cancellationToken)
        => chatSessionAppService.GetModelOptionsAsync(providerGroupId, cancellationToken);

    [HttpGet("{id}")]
    public Task<ChatSessionOutputDto> GetAsync(Guid id, CancellationToken cancellationToken)
        => chatSessionAppService.GetAsync(id, cancellationToken);

    [HttpGet("{id}/messages")]
    public Task<PagedResultDto<ChatMessageOutputDto>> GetMessagePagedListAsync(
        Guid id,
        [FromQuery] GetChatMessagePagedInputDto input,
        CancellationToken cancellationToken = default)
        => chatSessionAppService.GetMessagePagedListAsync(id, input, cancellationToken);

    [HttpPost]
    public Task<ChatSessionOutputDto> CreateAsync([FromBody] CreateChatSessionInputDto input, CancellationToken cancellationToken)
        => chatSessionAppService.CreateAsync(input, cancellationToken);

    [HttpPut("{id}")]
    public Task<ChatSessionOutputDto> UpdateAsync(Guid id, [FromBody] UpdateChatSessionInputDto input, CancellationToken cancellationToken)
        => chatSessionAppService.UpdateAsync(id, input, cancellationToken);

    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        => chatSessionAppService.DeleteAsync(id, cancellationToken);

    [HttpPost("{id}/messages")]
    public async Task SendMessageAsync(Guid id, [FromBody] SendChatMessageInputDto input, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var options = Domain.Shared.Json.JsonOptions.WebApi;
        var requestContext = new WorkspaceChatRequestContextDto(
            Headers: Request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToString(),
                StringComparer.OrdinalIgnoreCase),
            RequestUrl: Request.Path + Request.QueryString,
            ClientIp: HttpContext.Connection.RemoteIpAddress?.ToString());

        try
        {
            await foreach (var evt in chatSessionAppService.SendMessageAsync(id, input, requestContext, cancellationToken))
            {
                var data = JsonSerializer.Serialize(evt, options);
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {data}\n\n"), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "工作区聊天流式响应失败: SessionId={SessionId}", id);

            var errorEvt = new StreamEvent
            {
                Type = StreamEventType.Error,
                Content = $"连接意外中断: {ex.Message}",
                IsComplete = true
            };
            var data = JsonSerializer.Serialize(errorEvt, options);
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"data: {data}\n\n"), CancellationToken.None);
            await Response.Body.FlushAsync(CancellationToken.None);
        }

        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes("data: [DONE]\n\n"), cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}

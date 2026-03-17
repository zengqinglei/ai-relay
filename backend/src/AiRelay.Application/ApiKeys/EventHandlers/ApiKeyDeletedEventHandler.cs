using AiRelay.Domain.ApiKeys.Events;
using Leistd.EventBus.Core.EventHandler;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ApiKeys.EventHandlers;

public class ApiKeyDeletedEventHandler(ILogger<ApiKeyDeletedEventHandler> logger)
    : IEventHandler<ApiKeyDeletedEvent>
{
    public async Task HandleAsync(ApiKeyDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("处理 API Key 删除事件: {Name}", @event.Name);
        await Task.CompletedTask;
    }
}


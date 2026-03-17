using AiRelay.Domain.ApiKeys.Events;
using Leistd.EventBus.Core.EventHandler;
using Microsoft.Extensions.Logging;

namespace AiRelay.Application.ApiKeys.EventHandlers;

public class ApiKeyCreatedEventHandler(ILogger<ApiKeyCreatedEventHandler> logger)
    : IEventHandler<ApiKeyCreatedEvent>
{
    public async Task HandleAsync(ApiKeyCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("处理 API Key 创建事件: {Name}", @event.Name);
        await Task.CompletedTask;
    }
}


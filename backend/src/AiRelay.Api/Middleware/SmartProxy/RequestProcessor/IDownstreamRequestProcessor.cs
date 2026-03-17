using AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Api.Middleware.SmartProxy.RequestProcessor;

public interface IDownstreamRequestProcessor
{
    Task<DownRequestContext> ProcessAsync(
        HttpContext context,
        IChatModelHandler chatModelHandler,
        Guid apiKeyId,
        CancellationToken cancellationToken = default);
}

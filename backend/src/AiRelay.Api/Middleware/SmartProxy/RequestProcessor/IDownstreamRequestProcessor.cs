using AiRelay.Domain.Shared.ExternalServices.ModelClient;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;

namespace AiRelay.Api.Middleware.SmartProxy.RequestProcessor;

public interface IDownstreamRequestProcessor
{
    Task<DownRequestContext> ProcessAsync(
        HttpContext context,
        IChatModelHandler chatModelHandler,
        Guid apiKeyId,
        CancellationToken cancellationToken = default);
}
// Note: IChatModelHandler parameter retained for ExtractModelInfo routing.

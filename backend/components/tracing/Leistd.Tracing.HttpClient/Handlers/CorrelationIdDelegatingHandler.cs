using Leistd.Tracing.Core.Options;
using Leistd.Tracing.Core.Services;
using Microsoft.Extensions.Options;

namespace Leistd.Tracing.HttpClient.Handlers;

public class CorrelationIdDelegatingHandler(ICorrelationIdProvider correlationIdProvider, IOptions<CorrelationIdOptions> options) : DelegatingHandler
{
    private readonly CorrelationIdOptions _options = options.Value;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!_options.Enable)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var correlationId = correlationIdProvider.Get();
        if (!string.IsNullOrEmpty(correlationId))
        {
            var headers = _options.GetHttpHeaderNames();
            foreach (var headerName in headers)
            {
                if (!request.Headers.Contains(headerName))
                {
                    request.Headers.Add(headerName, correlationId);
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
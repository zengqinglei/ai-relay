using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Claude;

public interface IClaudeCodeClientDetector
{
    bool IsClaudeCodeClient(DownRequestContext down);
}

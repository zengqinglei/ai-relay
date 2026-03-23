using System.Text.Json.Nodes;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;

public interface IClaudeCodeClientDetector
{
    bool IsClaudeCodeClient(DownRequestContext down, JsonObject? requestJson);
}

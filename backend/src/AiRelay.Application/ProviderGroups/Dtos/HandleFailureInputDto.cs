using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

namespace AiRelay.Application.ProviderGroups.Dtos;

/// <summary>
/// 处理失败请求输入 DTO（仅用于状态更新）
/// </summary>
public record HandleFailureInputDto(
    Guid AccountId,
    int StatusCode,
    string? ErrorContent,
    string? DownModelId,
    string? UpModelId,
    ModelErrorAnalysisResult ErrorAnalysis);

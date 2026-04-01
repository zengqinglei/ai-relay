namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

/// <summary>
/// 连接验证结果
/// </summary>
/// <param name="IsSuccess">是否成功</param>
/// <param name="Error">错误信息</param>
/// <param name="ProjectId">获取到的项目 ID (如 Gemini Account)</param>
public record ConnectionValidationResult(
    bool IsSuccess,
    string? Error = null,
    string? ProjectId = null
);

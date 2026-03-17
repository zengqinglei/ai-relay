using AiRelay.Domain.ApiKeys.Entities;

namespace AiRelay.Application.ApiKeys.Dtos;

/// <summary>
/// API Key 验证结果
/// </summary>
public class ApiKeyValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public required bool IsValid { get; set; }

    /// <summary>
    /// API Key ID（验证成功时）
    /// </summary>
    public Guid? ApiKeyId { get; set; }

    /// <summary>
    /// API Key 名称（验证成功时）
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 失败原因（验证失败时）
    /// </summary>
    public string? FailureReason { get; set; }

    public static ApiKeyValidationResult Success(ApiKey apiKey)
    {
        return new ApiKeyValidationResult
        {
            IsValid = true,
            ApiKeyId = apiKey.Id,
            Name = apiKey.Name
        };
    }

    public static ApiKeyValidationResult Failure(string reason)
    {
        return new ApiKeyValidationResult
        {
            IsValid = false,
            FailureReason = reason
        };
    }
}

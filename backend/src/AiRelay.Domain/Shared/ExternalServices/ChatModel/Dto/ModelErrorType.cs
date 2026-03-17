namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;

public enum ModelErrorType
{
    Unknown,
    RateLimit,          // 429 or 503 with specific body
    SignatureError,     // Antigravity 400 Signature invalid
    ServerError,        // 500, 502, 504
    AuthenticationError,// 401, 403
    BadRequest,         // 400 (General)
    PromptTooLong       // 400 Prompt too long
}

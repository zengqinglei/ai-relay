using System.ComponentModel.DataAnnotations;
using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Application.ProviderAccounts.Dtos;

/// <summary>
/// 创建提供商账户输入 DTO
/// </summary>
public class CreateAccountTokenInputDto
{
    /// <summary>
    /// 账户名称
    /// </summary>
    [Display(Name = "账户名称")]
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "{0}长度必须在 {2}-{1} 个字符之间")]
    public required string Name { get; init; }

    /// <summary>
    /// 提供商
    /// </summary>
    [Display(Name = "提供商")]
    [Required(ErrorMessage = "{0}不能为空")]
    public Provider Provider { get; init; }

    /// <summary>
    /// 认证方式
    /// </summary>
    [Display(Name = "认证方式")]
    [Required(ErrorMessage = "{0}不能为空")]
    public AuthMethod AuthMethod { get; init; }

    /// <summary>
    /// 额外属性 (chatgpt_account_id, project_id)
    /// </summary>
    [Display(Name = "额外属性")]
    public Dictionary<string, string>? ExtraProperties { get; init; }

    /// <summary>
    /// Base URL
    /// </summary>
    [Display(Name = "BaseURL")]
    [MaxLength(512, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    [RegularExpression(@"^(?:https?://.+|)$", ErrorMessage = "{0}格式不正确")]
    public string? BaseUrl { get; init; }

    /// <summary>
    /// 描述说明
    /// </summary>
    [Display(Name = "描述")]
    [MaxLength(1000, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Description { get; init; }

    /// <summary>
    /// 凭证 (Access Token / API Key / Refresh Token)
    /// OAuth 模式下可选，API Key 模式下必填
    /// </summary>
    [Display(Name = "凭证")]
    [StringLength(2048, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Credential { get; init; }

    /// <summary>
    /// OAuth 授权码 (Gemini/Antigravity)
    /// </summary>
    public string? AuthCode { get; init; }

    /// <summary>
    /// OAuth 会话 ID (Gemini/Antigravity)
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// 最大并发数（0 表示不限制）
    /// </summary>
    [Display(Name = "最大并发数")]
    [Range(0, 1000, ErrorMessage = "{0}必须在 {1}-{2} 之间")]
    public int MaxConcurrency { get; init; } = 10;

    /// <summary>
    /// 调度优先级（值越小优先级越高）
    /// </summary>
    [Display(Name = "调度优先级")]
    [Range(1, 1000, ErrorMessage = "{0}必须在 {1}-{2} 之间")]
    public int Priority { get; init; } = 1;

    /// <summary>
    /// 调度权重（同优先级内按权重分配）
    /// </summary>
    [Display(Name = "调度权重")]
    [Range(1, 100, ErrorMessage = "{0}必须在 {1}-{2} 之间")]
    public int Weight { get; init; } = 50;

    /// <summary>
    /// 账户所属分组ID列表
    /// </summary>
    [Display(Name = "所属分组")]
    public List<Guid>? ProviderGroupIds { get; init; }

    /// <summary>
    /// 模型白名单
    /// </summary>
    public List<string>? ModelWhites { get; init; }

    /// <summary>
    /// 模型映射规则
    /// </summary>
    public Dictionary<string, string>? ModelMapping { get; init; }

    /// <summary>
    /// 限流控制范围（按账户 / 按模型）
    /// </summary>
    public RateLimitScope RateLimitScope { get; init; } = RateLimitScope.Account;

    /// <summary>
    /// 是否允许伪装为官方客户端
    /// </summary>
    public bool AllowOfficialClientMimic { get; init; } = false;

    /// <summary>
    /// 流健康度检查 (主动拦截没有输出或是带有错误抛出的残缺数据流)
    /// </summary>
    public bool IsCheckStreamHealth { get; init; } = false;
}

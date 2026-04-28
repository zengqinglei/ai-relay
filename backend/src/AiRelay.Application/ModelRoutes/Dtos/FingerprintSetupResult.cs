namespace AiRelay.Application.ModelRoutes.Dtos;

/// <summary>
/// 指纹设置结果（选号后初始化官方客户端仿真所需的会话和指纹信息）
/// </summary>
public record FingerprintSetupResult(string StickySessionId, string FingerprintClientId);

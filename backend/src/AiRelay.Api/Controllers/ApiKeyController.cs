using AiRelay.Application.ApiKeys.AppServices;
using AiRelay.Application.ApiKeys.Dtos;
using Leistd.Ddd.Application.Contracts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// API Key 管理控制器
/// </summary>
[Authorize]
[Route("api/v1/api-keys")]
public class ApiKeyController(IApiKeyAppService apiKeyAppService) : BaseController
{
    /// <summary>
    /// 获取 API Key 列表
    /// </summary>
    [HttpGet]
    public async Task<PagedResultDto<ApiKeyOutputDto>> GetPagedListAsync(
        [FromQuery] GetApiKeyPagedInputDto input,
        CancellationToken cancellationToken)
    {
        return await apiKeyAppService.GetPagedListAsync(input, cancellationToken);
    }

    /// <summary>
    /// 获取 API Key 详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ApiKeyOutputDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await apiKeyAppService.GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// 创建 API Key
    /// </summary>
    [HttpPost]
    public async Task<ApiKeyOutputDto> CreateAsync(
        [FromBody] CreateApiKeyInputDto input,
        CancellationToken cancellationToken)
    {
        return await apiKeyAppService.CreateAsync(input, cancellationToken);
    }

    /// <summary>
    /// 更新 API Key
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ApiKeyOutputDto> UpdateAsync(
        Guid id,
        [FromBody] UpdateApiKeyInputDto input,
        CancellationToken cancellationToken)
    {
        return await apiKeyAppService.UpdateAsync(id, input, cancellationToken);
    }

    /// <summary>
    /// 删除 API Key
    /// </summary>
    [HttpDelete("{id}")]
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await apiKeyAppService.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// 启用 API Key
    /// </summary>
    [HttpPatch("{id}/enable")]
    public async Task EnableAsync(
        Guid id,
        [FromBody] EnableApiKeyInputDto input,
        CancellationToken cancellationToken)
    {
        await apiKeyAppService.EnableAsync(id, input.ExpiresAt, cancellationToken);
    }

    /// <summary>
    /// 禁用 API Key
    /// </summary>
    [HttpPatch("{id}/disable")]
    public async Task DisableAsync(Guid id, CancellationToken cancellationToken)
    {
        await apiKeyAppService.DisableAsync(id, cancellationToken);
    }
}
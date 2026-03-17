using AiRelay.Application.ProviderGroups.AppServices;
using AiRelay.Application.ProviderGroups.Dtos;
using Leistd.Ddd.Application.Contracts.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiRelay.Api.Controllers;

/// <summary>
/// 提供商分组管理控制器
/// </summary>
[Authorize]
[Route("api/v1/provider-groups")]
public class ProviderGroupController(IProviderGroupAppService providerGroupAppService) : BaseController
{
    /// <summary>
    /// 获取分组列表
    /// </summary>
    [HttpGet]
    public async Task<PagedResultDto<ProviderGroupOutputDto>> GetPagedListAsync(
        [FromQuery] GetProviderGroupPagedInputDto input,
        CancellationToken cancellationToken)
    {
        return await providerGroupAppService.GetPagedListAsync(input, cancellationToken);
    }

    /// <summary>
    /// 获取分组详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ProviderGroupOutputDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await providerGroupAppService.GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// 创建分组
    /// </summary>
    [HttpPost]
    public async Task<ProviderGroupOutputDto> CreateAsync(
        [FromBody] CreateProviderGroupInputDto input,
        CancellationToken cancellationToken)
    {
        return await providerGroupAppService.CreateAsync(input, cancellationToken);
    }

    /// <summary>
    /// 更新分组
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ProviderGroupOutputDto> UpdateAsync(
        Guid id,
        [FromBody] UpdateProviderGroupInputDto input,
        CancellationToken cancellationToken)
    {
        return await providerGroupAppService.UpdateAsync(id, input, cancellationToken);
    }

    /// <summary>
    /// 删除分组
    /// </summary>
    [HttpDelete("{id}")]
    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await providerGroupAppService.DeleteAsync(id, cancellationToken);
    }
}
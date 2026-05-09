using System.Linq.Dynamic.Core;
using System.Security.Cryptography;
using System.Text.Json;
using AiRelay.Application.OpenApplications.Dtos;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Exception.Core;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace AiRelay.Application.OpenApplications.AppServices;

public class OpenApplicationAppService(
    IOpenIddictApplicationManager applicationManager,
    ILogger<OpenApplicationAppService> logger) : BaseAppService, IOpenApplicationAppService
{
    private const string CreationTimePropertyName = "creationTime";
    private const string PkceRequirement = "ft:pkce";

    private static readonly HashSet<string> ApplicationTypes = new(StringComparer.Ordinal)
    {
        OpenIddictConstants.ApplicationTypes.Web,
        OpenIddictConstants.ApplicationTypes.Native,
        "service"
    };

    private static readonly HashSet<string> ClientTypes = new(StringComparer.Ordinal)
    {
        OpenIddictConstants.ClientTypes.Public,
        OpenIddictConstants.ClientTypes.Confidential
    };

    private static readonly HashSet<string> ConsentTypes = new(StringComparer.Ordinal)
    {
        OpenIddictConstants.ConsentTypes.Explicit,
        OpenIddictConstants.ConsentTypes.External,
        OpenIddictConstants.ConsentTypes.Implicit,
        OpenIddictConstants.ConsentTypes.Systematic
    };

    public async Task<PagedResultDto<OpenApplicationOutputDto>> GetPagedListAsync(
        GetOpenApplicationPagedInputDto input,
        CancellationToken cancellationToken = default)
    {
        var intermediateItems = new List<IntermediateAppDto>();
        await foreach (var app in applicationManager.ListAsync(count: null, offset: null, cancellationToken))
        {
            intermediateItems.Add(new IntermediateAppDto
            {
                Application = app,
                ClientId = await applicationManager.GetClientIdAsync(app, cancellationToken) ?? string.Empty,
                DisplayName = await applicationManager.GetDisplayNameAsync(app, cancellationToken),
                ApplicationType = await applicationManager.GetApplicationTypeAsync(app, cancellationToken),
                ClientType = await applicationManager.GetClientTypeAsync(app, cancellationToken),
                ConsentType = await applicationManager.GetConsentTypeAsync(app, cancellationToken),
                CreationTime = GetCreationTime(await applicationManager.GetPropertiesAsync(app, cancellationToken))
            });
        }

        var query = intermediateItems.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            query = query.Where(item =>
                item.ClientId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (item.DisplayName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(input.ApplicationType))
        {
            query = query.Where(item => item.ApplicationType == input.ApplicationType);
        }

        if (!string.IsNullOrWhiteSpace(input.ClientType))
        {
            query = query.Where(item => item.ClientType == input.ClientType);
        }

        var sorting = input.Sorting ?? "clientId asc";
        var filteredItems = query.AsQueryable().OrderBy(sorting).ToList();
        var totalCount = filteredItems.Count;
        var pagedItems = filteredItems
            .Skip(Math.Max(input.Offset, 0))
            .Take(Math.Max(input.Limit, 0))
            .ToList();

        var outputItems = new List<OpenApplicationOutputDto>();
        foreach (var item in pagedItems)
        {
            outputItems.Add(await MapToOutputAsync(item.Application, cancellationToken));
        }

        return new PagedResultDto<OpenApplicationOutputDto>(totalCount, outputItems);
    }

    public async Task<OpenApplicationOutputDto> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var application = await FindRequiredAsync(id, cancellationToken);
        return await MapToOutputAsync(application, cancellationToken);
    }

    public async Task<OpenApplicationOutputDto> CreateAsync(
        CreateOpenApplicationInputDto input,
        CancellationToken cancellationToken = default)
    {
        var clientId = input.ClientId.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new BadRequestException("Client ID 不能为空");
        }

        if (await applicationManager.FindByClientIdAsync(clientId, cancellationToken) != null)
        {
            throw new BadRequestException($"Client ID 已存在: {clientId}");
        }

        ValidateApplication(input.ApplicationType, input.ClientType, input.ConsentType, input.ClientSecret, input.RedirectUris, input.PostLogoutRedirectUris, input.Requirements);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = string.IsNullOrWhiteSpace(input.DisplayName) ? null : input.DisplayName.Trim(),
            ApplicationType = input.ApplicationType,
            ClientType = input.ClientType,
            ConsentType = input.ConsentType,
            ClientSecret = input.ClientType == OpenIddictConstants.ClientTypes.Confidential ? input.ClientSecret : null
        };

        ApplyCollections(
            descriptor,
            input.RedirectUris,
            input.PostLogoutRedirectUris,
            input.Permissions,
            input.Requirements);
        descriptor.Properties[CreationTimePropertyName] = JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow);

        var application = await applicationManager.CreateAsync(descriptor, cancellationToken);
        logger.LogInformation("创建开放应用成功 (ClientId: {ClientId})", clientId);
        return await MapToOutputAsync(application, cancellationToken);
    }

    public async Task<OpenApplicationOutputDto> UpdateAsync(
        string id,
        UpdateOpenApplicationInputDto input,
        CancellationToken cancellationToken = default)
    {
        ValidateApplication(input.ApplicationType, input.ClientType, input.ConsentType, null, input.RedirectUris, input.PostLogoutRedirectUris, input.Requirements);

        var application = await FindRequiredAsync(id, cancellationToken);
        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);

        descriptor.DisplayName = string.IsNullOrWhiteSpace(input.DisplayName) ? null : input.DisplayName.Trim();
        descriptor.ApplicationType = input.ApplicationType;
        descriptor.ClientType = input.ClientType;
        descriptor.ConsentType = input.ConsentType;
        if (input.ClientType == OpenIddictConstants.ClientTypes.Public)
        {
            descriptor.ClientSecret = null;
        }

        ApplyCollections(
            descriptor,
            input.RedirectUris,
            input.PostLogoutRedirectUris,
            input.Permissions,
            input.Requirements);

        await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        logger.LogInformation("更新开放应用成功 (ID: {Id})", id);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var application = await FindRequiredAsync(id, cancellationToken);
        await applicationManager.DeleteAsync(application, cancellationToken);
        logger.LogInformation("删除开放应用成功 (ID: {Id})", id);
    }

    public async Task<ResetOpenApplicationSecretOutputDto> ResetSecretAsync(string id, CancellationToken cancellationToken = default)
    {
        var application = await FindRequiredAsync(id, cancellationToken);
        var clientType = await applicationManager.GetClientTypeAsync(application, cancellationToken);
        if (clientType != OpenIddictConstants.ClientTypes.Confidential)
        {
            throw new BadRequestException("只有 Confidential 客户端可以重置密钥");
        }

        var clientSecret = GenerateClientSecret();
        await applicationManager.UpdateAsync(application, clientSecret, cancellationToken);
        logger.LogInformation("重置开放应用密钥成功 (ID: {Id})", id);
        return new ResetOpenApplicationSecretOutputDto { ClientSecret = clientSecret };
    }

    private async Task<object> FindRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var application = await applicationManager.FindByIdAsync(id, cancellationToken);
        if (application == null)
        {
            throw new NotFoundException($"开放应用不存在: {id}");
        }

        return application;
    }

    private async Task<OpenApplicationOutputDto> MapToOutputAsync(object application, CancellationToken cancellationToken)
    {
        var id = await applicationManager.GetIdAsync(application, cancellationToken);
        var clientId = await applicationManager.GetClientIdAsync(application, cancellationToken);
        var applicationType = await applicationManager.GetApplicationTypeAsync(application, cancellationToken);
        var clientType = await applicationManager.GetClientTypeAsync(application, cancellationToken);
        var consentType = await applicationManager.GetConsentTypeAsync(application, cancellationToken);
        var redirectUris = await applicationManager.GetRedirectUrisAsync(application, cancellationToken);
        var postLogoutRedirectUris = await applicationManager.GetPostLogoutRedirectUrisAsync(application, cancellationToken);
        var permissions = await applicationManager.GetPermissionsAsync(application, cancellationToken);
        var requirements = await applicationManager.GetRequirementsAsync(application, cancellationToken);
        var settings = await applicationManager.GetSettingsAsync(application, cancellationToken);
        var properties = await applicationManager.GetPropertiesAsync(application, cancellationToken);
        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);

        return new OpenApplicationOutputDto
        {
            Id = id ?? string.Empty,
            ClientId = clientId ?? string.Empty,
            DisplayName = await applicationManager.GetDisplayNameAsync(application, cancellationToken),
            ApplicationType = applicationType ?? OpenIddictConstants.ApplicationTypes.Web,
            ClientType = clientType ?? OpenIddictConstants.ClientTypes.Public,
            ConsentType = consentType ?? OpenIddictConstants.ConsentTypes.Explicit,
            RedirectUris = redirectUris.Select(uri => uri.ToString()).ToList(),
            PostLogoutRedirectUris = postLogoutRedirectUris.Select(uri => uri.ToString()).ToList(),
            Permissions = permissions.ToList(),
            Requirements = requirements.ToList(),
            Settings = settings.ToDictionary(pair => pair.Key, pair => pair.Value),
            Properties = properties.ToDictionary(pair => pair.Key, pair => pair.Value),
            HasClientSecret = !string.IsNullOrEmpty(descriptor.ClientSecret),
            CreationTime = GetCreationTime(properties)
        };
    }

    private static void ValidateApplication(
        string applicationType,
        string clientType,
        string consentType,
        string? clientSecret,
        IReadOnlyCollection<string> redirectUris,
        IReadOnlyCollection<string> postLogoutRedirectUris,
        IReadOnlyCollection<string> requirements)
    {
        if (!ApplicationTypes.Contains(applicationType))
        {
            throw new BadRequestException($"应用类型不支持: {applicationType}");
        }

        if (!ClientTypes.Contains(clientType))
        {
            throw new BadRequestException($"客户端类型不支持: {clientType}");
        }

        if (!ConsentTypes.Contains(consentType))
        {
            throw new BadRequestException($"同意类型不支持: {consentType}");
        }

        if (clientType == OpenIddictConstants.ClientTypes.Public && !string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new BadRequestException("Public 客户端不能配置 Client Secret");
        }

        if ((applicationType == OpenIddictConstants.ApplicationTypes.Native || clientType == OpenIddictConstants.ClientTypes.Public) &&
            !requirements.Contains(PkceRequirement))
        {
            throw new BadRequestException("Native/Public 客户端必须启用 PKCE");
        }

        foreach (var uri in redirectUris.Concat(postLogoutRedirectUris))
        {
            ValidateUri(uri);
        }
    }

    private static void ValidateUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new BadRequestException($"URI 不合法: {value}");
        }
    }

    private static void ApplyCollections(
        OpenIddictApplicationDescriptor descriptor,
        IEnumerable<string> redirectUris,
        IEnumerable<string> postLogoutRedirectUris,
        IEnumerable<string> permissions,
        IEnumerable<string> requirements)
    {
        descriptor.RedirectUris.Clear();
        foreach (var uri in redirectUris.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            descriptor.RedirectUris.Add(new Uri(uri, UriKind.Absolute));
        }

        descriptor.PostLogoutRedirectUris.Clear();
        foreach (var uri in postLogoutRedirectUris.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri, UriKind.Absolute));
        }

        descriptor.Permissions.Clear();
        foreach (var permission in permissions.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            descriptor.Permissions.Add(permission);
        }

        descriptor.Requirements.Clear();
        foreach (var requirement in requirements.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            descriptor.Requirements.Add(requirement);
        }
    }

    private static DateTimeOffset GetCreationTime(IReadOnlyDictionary<string, JsonElement> properties)
    {
        if (!properties.TryGetValue(CreationTimePropertyName, out var value))
        {
            return DateTimeOffset.MinValue;
        }

        if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var dateTime))
        {
            return dateTime;
        }

        if (value.TryGetDateTimeOffset(out dateTime))
        {
            return dateTime;
        }

        return DateTimeOffset.MinValue;
    }

    private static string GenerateClientSecret()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    private class IntermediateAppDto
    {
        public required object Application { get; init; }
        public required string ClientId { get; init; }
        public string? DisplayName { get; init; }
        public string? ApplicationType { get; init; }
        public string? ClientType { get; init; }
        public string? ConsentType { get; init; }
        public DateTimeOffset CreationTime { get; init; }
    }
}

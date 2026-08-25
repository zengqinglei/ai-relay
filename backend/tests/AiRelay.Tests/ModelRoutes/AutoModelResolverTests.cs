using AiRelay.Application.ApiKeys.Options;
using AiRelay.Application.ModelRoutes;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using Leistd.Exception.Core;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiRelay.Tests.ModelRoutes;

/// <summary>
/// auto 模型解析：候选列表构建、粘性缓存的读写、failover 上下文初始化。
/// 该逻辑供代理中间件与工作区聊天两条链路共用。
/// </summary>
public class AutoModelResolverTests
{
    private static AutoModelResolver CreateResolver(
        IDistributedCache? cache = null, params string[] models)
    {
        var options = new DefaultProviderModelsOptions
        {
            Models = models.Length > 0 ? models : ["model-a", "model-b", "model-c"]
        };
        cache ??= new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new AutoModelResolver(Options.Create(options), cache);
    }

    private static DownRequestContext CreateContext(string? modelId, string? sessionId = "session-1") =>
        new() { ModelId = modelId, SessionId = sessionId };

    [Fact]
    public async Task ResolveAsync_NonAutoModel_ReturnsNull_AndLeavesContextUntouched()
    {
        var resolver = CreateResolver();
        var context = CreateContext("gpt-4o");

        var failover = await resolver.ResolveAsync(context, CancellationToken.None);

        Assert.Null(failover);
        Assert.Null(context.ResolvedModelId);
    }

    [Fact]
    public async Task ResolveAsync_Auto_StartsFromFirstCandidate_WhenNoStickyCache()
    {
        var resolver = CreateResolver();
        var context = CreateContext("auto");

        var failover = await resolver.ResolveAsync(context, CancellationToken.None);

        Assert.NotNull(failover);
        Assert.Equal("model-a", context.ResolvedModelId);
        Assert.Equal(0, failover.CurrentModelIndex);
        Assert.Equal(["model-a", "model-b", "model-c"], failover.CandidateModels);
    }

    [Fact]
    public async Task ResolveAsync_Auto_ExcludesAutoFromCandidates()
    {
        var resolver = CreateResolver(cache: null, "AUTO", "model-a");
        var context = CreateContext("auto");

        var failover = await resolver.ResolveAsync(context, CancellationToken.None);

        Assert.NotNull(failover);
        Assert.Equal(["model-a"], failover.CandidateModels);
    }

    [Fact]
    public async Task ResolveAsync_Auto_NoValidCandidates_ThrowsBadRequest()
    {
        var resolver = CreateResolver(cache: null, "auto");
        var context = CreateContext("auto");

        await Assert.ThrowsAsync<BadRequestException>(
            () => resolver.ResolveAsync(context, CancellationToken.None));
    }

    [Fact]
    public async Task SaveStickyModelAsync_ThenResolve_StartsFromSavedModel()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var resolver = CreateResolver(cache);

        var first = CreateContext("auto");
        var failover = await resolver.ResolveAsync(first, CancellationToken.None);
        first.ResolvedModelId = "model-b";
        await resolver.SaveStickyModelAsync(first, failover, CancellationToken.None);

        var second = CreateContext("auto");
        var secondFailover = await resolver.ResolveAsync(second, CancellationToken.None);

        Assert.NotNull(secondFailover);
        Assert.Equal("model-b", second.ResolvedModelId);
        Assert.Equal(1, secondFailover.CurrentModelIndex);
    }

    [Fact]
    public async Task ResolveAsync_Auto_IgnoresStickyModelNotInCandidates()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var resolver = CreateResolver(cache);

        var first = CreateContext("auto");
        var failover = await resolver.ResolveAsync(first, CancellationToken.None);
        first.ResolvedModelId = "removed-model";
        await resolver.SaveStickyModelAsync(first, failover, CancellationToken.None);

        var second = CreateContext("auto");
        var secondFailover = await resolver.ResolveAsync(second, CancellationToken.None);

        Assert.NotNull(secondFailover);
        Assert.Equal("model-a", second.ResolvedModelId);
    }

    [Fact]
    public async Task SaveStickyModelAsync_NonFailoverRequest_DoesNothing()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var resolver = CreateResolver(cache);

        var context = CreateContext("gpt-4o");
        context.ResolvedModelId = "should-not-be-saved";
        await resolver.SaveStickyModelAsync(context, failoverContext: null, CancellationToken.None);

        var probe = CreateContext("auto");
        await resolver.ResolveAsync(probe, CancellationToken.None);
        Assert.Equal("model-a", probe.ResolvedModelId);
    }
}

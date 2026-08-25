using AiRelay.Application.ModelRoutes;
using Leistd.Exception.Core;
using Xunit;

namespace AiRelay.Tests.ModelRoutes;

/// <summary>
/// 候选账号耗尽时的异常语义：
///   - 候选中没有任何账号声明支持该模型 => 404（模型未开通，客户端不应重试）
///   - 有账号支持但当前均临时不可用（冷却/限流/被排除） => 503（临时过载，客户端可重试）
/// </summary>
public class NoAccountExceptionClassificationTests
{
    [Fact]
    public void NoAccountSupportsModel_Returns404NotFound()
    {
        var ex = ModelRouteAppService.CreateNoAccountForModelException(
            anyAccountSupportsModel: false, modelId: "gpt-x");

        Assert.IsType<NotFoundException>(ex);
        Assert.Contains("gpt-x", ex.Message);
    }

    [Fact]
    public void AccountsSupportModelButTemporarilyUnavailable_Returns503ServiceUnavailable()
    {
        var ex = ModelRouteAppService.CreateNoAccountForModelException(
            anyAccountSupportsModel: true, modelId: "gpt-x");

        Assert.IsType<ServiceUnavailableException>(ex);
        Assert.Contains("gpt-x", ex.Message);
    }
}

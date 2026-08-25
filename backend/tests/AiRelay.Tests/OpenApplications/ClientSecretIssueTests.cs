using AiRelay.Application.OpenApplications.AppServices;
using Xunit;

namespace AiRelay.Tests.OpenApplications;

/// <summary>
/// 创建开放应用时的密钥签发规则：
///   - Confidential 客户端由服务端生成高熵密钥（创建响应中一次性返回）
///   - Public 客户端不签发密钥
/// </summary>
public class ClientSecretIssueTests
{
    [Fact]
    public void Confidential_IssuesHighEntropySecret()
    {
        var first = OpenApplicationAppService.IssueClientSecret("confidential");
        var second = OpenApplicationAppService.IssueClientSecret("confidential");

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.False(string.IsNullOrWhiteSpace(second));
        // 32 字节随机数的 Base64 长度为 44
        Assert.Equal(44, first!.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Public_DoesNotIssueSecret()
    {
        Assert.Null(OpenApplicationAppService.IssueClientSecret("public"));
    }
}

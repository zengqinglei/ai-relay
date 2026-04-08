using System.Net.Http;
using System.IO.Compression;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Interceptors;

/// <summary>
/// 专职处理上游返回的手工压缩流拦截器
/// 在为了伪造端点指纹手动设置了 Accept-Encoding 导致底层自动解压失效时，由本中间件将响应透明解压。
/// </summary>
public class ManualDecompressionHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        var encoding = response.Content?.Headers.ContentEncoding.FirstOrDefault()?.ToLowerInvariant();
        if (response.Content != null && (encoding == "br" || encoding == "gzip" || encoding == "deflate"))
        {
            var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            Stream decompressedStream = encoding switch
            {
                "br"      => new BrotliStream(networkStream, CompressionMode.Decompress),
                "gzip"    => new GZipStream(networkStream, CompressionMode.Decompress),
                "deflate" => new DeflateStream(networkStream, CompressionMode.Decompress),
                _         => networkStream
            };

            var decompressedContent = new StreamContent(decompressedStream);
            foreach (var header in response.Content.Headers)
            {
                // 剥离压缩相关的元数据头，避免上层解析冲突
                if (!header.Key.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase) &&
                    !header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    decompressedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            response.Content = decompressedContent;
        }

        return response;
    }
}

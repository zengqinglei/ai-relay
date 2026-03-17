using System.Text;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;

/// <summary>
/// SSE 流缓冲器，负责将字节块转换为完整的文本行
/// </summary>
public class SseStreamBuffer
{
    private readonly StringBuilder _buffer = new();

    /// <summary>
    /// 处理新的字节块，返回提取出的完整行
    /// </summary>
    public List<string> ProcessChunk(ReadOnlySpan<byte> chunk)
    {
        var text = Encoding.UTF8.GetString(chunk);
        _buffer.Append(text);

        return ExtractLines();
    }

    /// <summary>
    /// 刷新缓冲区，返回残留的未完成行（流结束时调用）
    /// </summary>
    public List<string> Flush()
    {
        if (_buffer.Length == 0) return [];

        var remaining = _buffer.ToString().Trim();
        _buffer.Clear();

        return string.IsNullOrEmpty(remaining) ? [] : [remaining];
    }

    private List<string> ExtractLines()
    {
        var results = new List<string>();
        int start = 0;

        for (int i = 0; i < _buffer.Length; i++)
        {
            if (_buffer[i] == '\n')
            {
                if (i > start)
                {
                    var line = _buffer.ToString(start, i - start).Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        results.Add(line);
                    }
                }
                start = i + 1;
            }
        }

        if (start > 0)
        {
            _buffer.Remove(0, start);
        }

        return results;
    }
}

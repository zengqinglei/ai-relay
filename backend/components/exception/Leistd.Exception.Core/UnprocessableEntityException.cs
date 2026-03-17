using System.Text.Json;

namespace Leistd.Exception.Core;

/// <summary>
/// 实体验证错误异常，默认错误码：42200
/// </summary>
public class UnprocessableEntityException(
    Dictionary<string, string[]> validationErrors,
    string message = "输入的信息有误",
    System.Exception? innerException = null)
    : BusinessException("422", message, innerException)
{
    /// <summary>
    /// 实体验证错误集合（符合 RFC 7807 ValidationProblemDetails 格式）
    /// </summary>
    public Dictionary<string, string[]>? ValidationErrors { get; private set; } = validationErrors;

    /// <summary>
    /// 便捷构造函数：单个字段单个错误
    /// </summary>
    public UnprocessableEntityException(string field, string error, System.Exception? innerException = null)
        : this(new Dictionary<string, string[]> { { field, new[] { error } } }, "输入的信息有误", innerException)
    {
    }

    /// <summary>
    /// 便捷构造函数：单个字段多个错误
    /// </summary>
    public UnprocessableEntityException(string field, string[] errors, System.Exception? innerException = null)
        : this(new Dictionary<string, string[]> { { field, errors } }, "输入的信息有误", innerException)
    {
    }


    /// <summary>
    /// 设置实体验证错误集合
    /// </summary>
    public UnprocessableEntityException WithErrors(Dictionary<string, string[]> validationErrors)
    {
        this.ValidationErrors = validationErrors;
        return this;
    }

    /// <summary>
    /// 添加单个字段的单个错误
    /// </summary>
    public UnprocessableEntityException AddError(string field, string error)
    {
        this.ValidationErrors ??= new Dictionary<string, string[]>();

        if (this.ValidationErrors.ContainsKey(field))
        {
            var existingErrors = this.ValidationErrors[field].ToList();
            existingErrors.Add(error);
            this.ValidationErrors[field] = existingErrors.ToArray();
        }
        else
        {
            this.ValidationErrors[field] = new[] { error };
        }

        return this;
    }

    /// <summary>
    /// 添加单个字段的多个错误
    /// </summary>
    public UnprocessableEntityException AddErrors(string field, params string[] errors)
    {
        this.ValidationErrors ??= new Dictionary<string, string[]>();

        if (this.ValidationErrors.ContainsKey(field))
        {
            var existingErrors = this.ValidationErrors[field].ToList();
            existingErrors.AddRange(errors);
            this.ValidationErrors[field] = existingErrors.ToArray();
        }
        else
        {
            this.ValidationErrors[field] = errors;
        }

        return this;
    }

    public override string ToString()
    {
        return this.GetType().Name + " : [code=" + Code + ", message=" + Message + ", details=" + Details + ", errors=" + JsonSerializer.Serialize(ValidationErrors) + "]";
    }
}

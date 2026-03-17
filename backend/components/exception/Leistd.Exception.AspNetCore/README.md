# Leistd.Exception.Web

ASP.NET Core全局异常处理组件，提供统一的异常处理和响应格式。

## 功能特性

- 统一的异常处理和响应格式
- 自动将各类异常转换为业务异常
- 支持验证异常的特殊处理
- 可配置的详细信息显示（开发/生产环境）
- 支持排除特定路径的异常处理
- 完整的日志记录

## 使用方法

### 1. 安装依赖

在您的ASP.NET Core项目中引用此项目：

```xml
<ProjectReference Include="path\to\Leistd.Exception.Web\Leistd.Exception.Web.csproj" />
```

### 2. 配置服务

在 `Program.cs` 或 `Startup.cs` 中注册服务：

```csharp
// 方式1: 从配置文件读取
builder.Services.AddGlobalExceptionHandler(builder.Configuration);

// 方式2: 使用委托配置
builder.Services.AddGlobalExceptionHandler(options =>
{
    options.Enable = true;
    options.IsShowDetails = null; // null表示开发环境显示，生产环境不显示
    options.ExcludePatterns.Add("/health");
    options.ExcludePatterns.Add("/api/health/**");
});
```

### 3. 使用中间件

在 `Program.cs` 中添加中间件（应该尽早添加）：

```csharp
var app = builder.Build();

// 在其他中间件之前添加
app.UseGlobalExceptionHandler();

// 其他中间件...
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

### 4. 配置文件设置（可选）

在 `appsettings.json` 中配置：

```json
{
  "Leistd": {
    "GlobalException": {
      "Enable": true,
      "IsShowDetails": null,
      "ExcludePatterns": [
        "/health",
        "/api/health/**"
      ]
    }
  }
}
```

## 配置说明

### GlobalExceptionOptions

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| Enable | bool | false | 是否启用全局异常处理 |
| ExcludePatterns | HashSet<string> | 空 | 排除的URI模式，支持通配符 |
| IsShowDetails | bool? | null | 是否显示详情，null表示根据环境自动决定 |

### 通配符模式

- `*` - 匹配单个路径段中的任意字符（不包括 `/`）
- `**` - 匹配任意路径（包括子路径）

示例：
- `/api/health` - 精确匹配
- `/api/*/test` - 匹配 `/api/v1/test`, `/api/v2/test` 等
- `/api/health/**` - 匹配 `/api/health` 下的所有路径

## 响应格式

异常会被转换为统一的JSON响应：

```json
{
  "success": false,
  "code": 40000,
  "message": "错误消息",
  "details": "详细错误信息（仅开发环境或配置启用时显示）",
  "errors": [
    {
      "field": "fieldName",
      "message": "验证错误消息"
    }
  ]
}
```

## HTTP状态码映射

异常的错误码会自动映射到HTTP状态码：
- `40000-40099` → 400 Bad Request
- `40100-40199` → 401 Unauthorized
- `40300-40399` → 403 Forbidden
- `40400-40499` → 404 Not Found
- `42200-42299` → 422 Unprocessable Entity
- `50000-50099` → 500 Internal Server Error

## 自定义异常处理器

如果需要自定义异常处理逻辑，实现 `IExceptionHandler` 接口：

```csharp
public class CustomExceptionHandler : IExceptionHandler
{
    public async Task<IResult> HandleAsync(Exception exception, HttpContext context)
    {
        // 自定义处理逻辑
        return Results.Json(new { error = exception.Message });
    }
}

// 注册
builder.Services.AddSingleton<IExceptionHandler, CustomExceptionHandler>();
```

## 示例

```csharp
// Controller中抛出异常
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult GetUser(int id)
    {
        if (id <= 0)
        {
            throw new BadRequestException("用户ID必须大于0");
        }

        var user = FindUser(id);
        if (user == null)
        {
            throw new NotFoundException($"未找到ID为{id}的用户");
        }

        return Ok(user);
    }
}

// 客户端收到的响应（400 Bad Request）
{
  "success": false,
  "code": 40000,
  "message": "用户ID必须大于0",
  "details": null,
  "errors": null
}
```

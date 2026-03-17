# 后端开发规范

本文档为基于 .NET 10、EF Core、DDD 架构的后端项目开发规范。

> **注意**: 本文档中的所有开发活动，都必须同时遵循 **[项目通用开发规范](./common_develop.md)** 中定义的 Git 工作流和提交规范。

---

## 目录

1. [核心技术栈](#1-核心技术栈)
2. [分层架构规范](#2-分层架构规范)
3. [编码规范](#3-编码规范)
4. [命名规范](#4-命名规范)
5. [数据访问规范](#5-数据访问规范)
6. [异常处理与日志](#6-异常处理与日志)

---

## 1. 核心技术栈

- **框架**: .NET 10+
- **ORM**: EF Core 10+
- **数据库**: PostgreSQL 15+
- **缓存**: Redis 7+
- **对象映射**: AutoMapper
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **日志**: Serilog

---

## 2. 分层架构规范

项目采用 DDD（领域驱动设计）分层架构：

```
┌─────────────────────────────────────────┐
│         API Layer (AiRelay.Api)         │  ← HTTP 相关处理
├─────────────────────────────────────────┤
│   Application Layer (Application)       │  ← 业务流程编排
├─────────────────────────────────────────┤
│      Domain Layer (Domain)              │  ← 核心业务逻辑
└─────────────────────────────────────────┘
         ↑
         │ 依赖
         │
┌─────────────────────────────────────────┐
│  Infrastructure Layer (Infrastructure)  │  ← 技术实现
└─────────────────────────────────────────┘
```

### 2.1 各层职责

#### API Layer（AiRelay.Api）
- **职责**: HTTP 相关处理（HttpContext、请求响应、路由）
- **原则**: HTTP 相关信息不应传递到 Application 层，保障 Application 层可用于多种架构（BS/CS）
- **包含**: Controllers、Middleware、Authentication

#### Application Layer（AiRelay.Application）
- **职责**: 协调业务逻辑（调用领域对象行为、领域服务、发布/订阅事件）
- **包含**: AppServices、Dtos、Mappings、EventHandlers
- **可以**: 使用 EF Core 的 `Include`、`GetQueryIncludingAsync` 进行数据查询和聚合

#### Domain Layer（AiRelay.Domain）
- **职责**: 核心业务逻辑（领域对象行为、领域服务、业务规则）
- **包含**: Entities、ValueObjects、DomainServices、Events、Specifications
- **接口定义**: 第三方服务接口、持久化接口（IRepository）
- **严格禁止**:
  - 引用 `Microsoft.EntityFrameworkCore`
  - 使用 EF Core 特性（Include、ThenInclude）
  - 领域服务之间相互依赖

#### Infrastructure Layer（AiRelay.Infrastructure）
- **职责**: 数据持久化、第三方服务对接实现
- **包含**: EF Core Configurations、Repositories、ExternalServices、Caching

---

## 3. 编码规范

### 3.1 .NET 10 新特性（强制使用）

| 特性 | 使用场景 | 示例 |
|------|---------|------|
| **主构造函数** | 所有服务类 | `public class UserService(IRepository<User> repo) { }` |
| **record 类型** | 所有 DTO | `public record CreateUserInputDto { }` |
| **init 属性** | DTO 属性 | `public string Name { get; init; }` |
| **required 修饰符** | DTO 必填属性 | `public required string Name { get; init; }` |
| **文件范围 namespace** | 所有文件 | `namespace AiRelay.Application.Users;` |

**示例**:
```csharp
namespace AiRelay.Application.Users;

/// <summary>
/// 用户应用服务
/// </summary>
public class UserAppService(
    IRepository<User, Guid> userRepository,
    UserDomainService userDomainService,
    IObjectMapper objectMapper) : IAppService
{
    public async Task<UserOutputDto> CreateAsync(
        CreateUserInputDto input,
        CancellationToken cancellationToken = default)
    {
        var user = await userDomainService.CreateUserAsync(
            input.Username, input.Email, cancellationToken);

        var result = objectMapper.Map<User, UserOutputDto>(user);
        return result;
    }
}
```

### 3.2 异步编程

- **强制异步**: 所有 I/O 操作必须使用 `async/await`
- **命名约定**: 异步方法名必须以 `Async` 结尾
- **禁止阻塞**: 不得使用 `.Result` 或 `.Wait()`

### 3.3 充血模型设计

**实体设计原则**:
1. ✅ 属性使用 `private set`，封装内部状态
2. ✅ 必须有 `private` 无参构造函数（EF Core 需要）
3. ✅ 通过公共方法修改状态
4. ✅ 构造函数中进行必要的业务验证
5. ✅ 包含业务方法（如 `Enable()`, `Disable()`）

**示例**:
```csharp
namespace AiRelay.Domain.Users.Entities;

public class User : FullAuditedEntity<Guid>
{
    public string Username { get; private set; }
    public string Email { get; private set; }
    public bool IsActive { get; private set; } = true;

    // EF Core 构造函数
    private User()
    {
        Username = null!;
        Email = null!;
    }

    // 业务构造函数
    public User(string username, string email)
    {
        Id = Guid.NewGuid();
        Username = username;
        Email = email;
        CreationTime = DateTime.UtcNow;
    }

    // 业务方法
    public void Enable() => IsActive = true;
    public void Disable() => IsActive = false;
}
```

### 3.4 领域服务规范

**命名**: `*DomainService`（无需定义接口）
**注入命名**: `userDomainService`（camelCase）

**职责范围**:
- ✅ 跨实体的业务逻辑
- ✅ 复杂业务规则验证
- ✅ 实体的创建、更新、删除的核心逻辑
- ❌ DTO 转换
- ❌ 事务管理
- ❌ 数据查询和聚合

**示例**:
```csharp
namespace AiRelay.Domain.Users.DomainServices;

public class UserDomainService(
    IRepository<User, Guid> userRepository,
    IPasswordHasher passwordHasher)
{
    public async Task<User> CreateUserAsync(
        string username,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        // 唯一性校验
        if (await userRepository.AnyAsync(u => u.Username == username, cancellationToken))
            throw new BadRequestException($"用户名 '{username}' 已存在");

        // 密码哈希
        var passwordHash = passwordHasher.HashPassword(password);

        // 创建用户
        var user = new User(username, email, passwordHash);
        await userRepository.InsertAsync(user, cancellationToken: cancellationToken);

        return user;
    }
}
```

### 3.5 应用服务规范

**命名**: 接口 `I*AppService`，实现 `*AppService`
**基类**: 继承 `IAppService`
**注入命名**:
- 仓储：`userRepository`（camelCase）
- 领域服务：`userDomainService`（camelCase）

**职责**:
- ✅ 接收/返回 DTO
- ✅ 调用领域服务和仓储
- ✅ 事务管理（通过 UnitOfWork）
- ✅ DTO 映射（使用 AutoMapper）
- ✅ 数据查询和聚合
- ❌ 核心业务规则（应在领域层）

**示例**:
```csharp
namespace AiRelay.Application.Users.AppServices;

public class UserAppService(
    IRepository<User, Guid> userRepository,
    UserDomainService userDomainService,
    IObjectMapper objectMapper) : IAppService
{
    public async Task<PagedResultDto<UserOutputDto>> GetPagedListAsync(
        GetUserPagedInputDto input,
        CancellationToken cancellationToken = default)
    {
        var query = await userRepository.GetQueryableAsync(cancellationToken);

        // 应用过滤条件
        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            query = query.Where(u => u.Username.Contains(input.Keyword));
        }

        // 获取总数
        var totalCount = await query.CountAsync(cancellationToken);

        // 分页查询
        var users = await query
            .OrderByDescending(u => u.CreationTime)
            .Skip(input.Offset)
            .Take(input.Limit)
            .ToListAsync(cancellationToken);

        // 映射
        var result = objectMapper.Map<List<User>, List<UserOutputDto>>(users);

        return new PagedResultDto<UserOutputDto>(totalCount, result);
    }
}
```

### 3.6 Controller 规范

**命名**: `*Controller`
**基类**: 继承 `BaseController`
**返回值**: 直接返回对象，不使用 `ActionResult<T>` 包装

**方法命名规范**:
| 操作类型 | 方法名 | HTTP 方法 | 路由 |
|---------|-------|----------|------|
| 分页查询 | `GetPageAsync` | GET | `/api/xxx` |
| 单个查询 | `GetAsync` | GET | `/api/xxx/{id}` |
| 创建 | `CreateAsync` | POST | `/api/xxx` |
| 更新 | `UpdateAsync` | PUT | `/api/xxx/{id}` |
| 删除 | `DeleteAsync` | DELETE | `/api/xxx/{id}` |

**示例**:
```csharp
namespace AiRelay.Api.Controllers;

[Authorize]
public class UserController(IUserAppService userAppService) : BaseController
{
    [HttpGet]
    public async Task<PagedResultDto<UserOutputDto>> GetPageAsync(
        [FromQuery] GetUserPagedInputDto input,
        CancellationToken cancellationToken)
    {
        return await userAppService.GetPagedListAsync(input, cancellationToken);
    }

    [HttpPost]
    public async Task<UserOutputDto> CreateAsync(
        [FromBody] CreateUserInputDto input,
        CancellationToken cancellationToken)
    {
        return await userAppService.CreateAsync(input, cancellationToken);
    }

    [HttpDelete("{id}")]
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await userAppService.DeleteAsync(id, cancellationToken);
    }
}
```

---

## 4. 命名规范

### 4.1 DTO 命名规范

| DTO 类型 | 命名规范 | 示例 |
|---------|---------|------|
| 分页查询输入 | `Get{Entity}PagedInputDto` | `GetUserPagedInputDto` |
| 创建输入 | `Create{Entity}InputDto` | `CreateUserInputDto` |
| 更新输入 | `Update{Entity}InputDto` | `UpdateUserInputDto` |
| 输出 | `{Entity}OutputDto` | `UserOutputDto` |

**分页 DTO 示例**:
```csharp
namespace AiRelay.Application.Users.Dtos;

/// <summary>
/// 获取用户分页列表输入 DTO
/// </summary>
public record GetUserPagedInputDto : PagedRequestDto
{
    [Display(Name = "搜索关键字")]
    [MaxLength(256, ErrorMessage = "{0}长度不能超过 {1} 个字符")]
    public string? Keyword { get; init; }

    [Display(Name = "是否启用")]
    public bool? IsActive { get; init; }
}
```

**DTO 验证规范**:
- ✅ 使用 Data Annotations 进行模型验证
- ✅ 错误消息使用占位符（`{0}不能为空`）
- ✅ 所有属性必须添加 `[Display(Name = "xxx")]`
- ✅ 必填属性使用 `required` 修饰符

### 4.2 变量命名规范

**方法参数**:
```csharp
// ✅ 正确：Dto 参数统一命名为 input
public async Task<UserOutputDto> CreateAsync(
    CreateUserInputDto input,
    CancellationToken cancellationToken = default)
```

**返回变量**:
```csharp
// ✅ 正确：返回对象统一命名为 result
var result = objectMapper.Map<User, UserOutputDto>(user);
return result;
```

**查询变量**:
```csharp
// ✅ 正确：query 变量命名为 query 或 xxxQuery
var query = await userRepository.GetQueryableAsync();
var users = await query.Where(...).ToListAsync();
```

### 4.3 仓储注入命名规范

```csharp
// ✅ 正确
public class UserAppService(
    IRepository<User, Guid> userRepository,          // ✅
    IRepository<Role, Guid> roleRepository,          // ✅
    UserDomainService userDomainService,             // ✅
    IObjectMapper objectMapper) : IAppService
{
}
```

---

## 5. 数据访问规范

### 5.1 Leistd 框架能力优先

**优先使用 Leistd 框架已有能力**:
- ✅ 分页查询使用 `GetPagedListAsync`（来自 `Leistd.Ddd.Infrastructure.Repositories.EfCoreRepository`）
- ✅ IQueryable 异步扩展使用 `Leistd.Ddd.Infrastructure.Repositories` 提供的方法
- ✅ 实体基类使用 `Entity<TKey>`、`FullAuditedEntity<TKey>` 等
- ✅ DTO 映射使用 AutoMapper（参考 `Mappings/*Profile`）

### 5.2 仓储常用方法

| 方法 | 说明 |
|------|------|
| `GetByIdAsync(id)` | 根据 ID 获取单个实体 |
| `GetListAsync(predicate)` | 根据条件获取列表 |
| `GetQueryableAsync()` | 获取 IQueryable 用于复杂查询 |
| `GetQueryIncludingAsync(...)` | 获取带 Include 的 IQueryable |
| `InsertAsync(entity)` | 插入实体 |
| `UpdateAsync(entity)` | 更新实体 |
| `DeleteAsync(entity)` | 删除实体（软删除） |
| `AnyAsync(predicate)` | 判断是否存在 |

### 5.3 应用层使用 EF Core 规范

**规范**:
- ✅ 可以使用 `GetQueryableAsync` + `Include` 加载导航属性
- ✅ 可以使用 `GetQueryIncludingAsync` 扩展方法
- ✅ 使用 `using Leistd.Ddd.Infrastructure.Repositories;` 引入异步扩展
- ❌ 禁止在 Application/Domain 层引入 `using Microsoft.EntityFrameworkCore;`

**示例**:
```csharp
using Leistd.Ddd.Infrastructure.Repositories;  // 引入扩展方法

var query = await apiKeyRepository.GetQueryIncludingAsync(
    k => k.Bindings,
    k => k.Bindings.Select(b => b.ProviderGroup));

var apiKeys = await query
    .Where(k => k.UserId == userId)
    .ToListAsync(cancellationToken);
```

### 5.4 分页查询命名规范

**方法命名**: 必须使用 `GetPagedListAsync`（而非 `GetListAsync`）

```csharp
// ✅ 正确
public async Task<PagedResultDto<UserOutputDto>> GetPagedListAsync(
    GetUserPagedInputDto input,
    CancellationToken cancellationToken = default)
{
    // ...
    return new PagedResultDto<UserOutputDto>(totalCount, result);
}
```

### 5.5 避免重复验证

**原则**: DTO 已通过 Data Annotations 验证时，领域实体无需重复验证

```csharp
// DTO 层已验证
public record CreateUserInputDto
{
    [Required(ErrorMessage = "{0}不能为空")]
    [StringLength(64, MinimumLength = 3)]
    public required string Username { get; init; }
}

// 领域实体无需重复验证
public User(string username)
{
    Id = Guid.NewGuid();
    Username = username;  // 无需 Check.NotNullOrWhiteSpace
}
```

---

## 6. 异常处理与日志

### 6.1 异常类型

使用 `Leistd.Exception.Core` 提供的异常类型:

| 异常类型 | HTTP 状态码 | 使用场景 |
|---------|-----------|---------|
| `BadRequestException` | 400 | 业务规则验证失败 |
| `NotFoundException` | 404 | 资源不存在 |
| `UnauthorizedException` | 401 | 未授权 |
| `ForbiddenException` | 403 | 无权限 |

**示例**:
```csharp
// 业务规则验证失败
if (await userRepository.AnyAsync(u => u.Username == username))
    throw new BadRequestException($"用户名 '{username}' 已存在");

// 资源不存在
var user = await userRepository.GetByIdAsync(id);
if (user == null)
    throw new NotFoundException($"用户 {id} 不存在");
```

### 6.2 日志记录

**使用场景**:
```csharp
// Information: 关键业务操作
logger.LogInformation("创建用户成功: {Username}", user.Username);

// Warning: 潜在问题
logger.LogWarning("用户 {UserId} 登录失败次数过多", userId);

// Error: 异常错误
logger.LogError(ex, "创建用户失败: {Username}", input.Username);
```

**最佳实践**:
- ✅ 使用结构化日志（消息模板 + 参数）
- ✅ 记录关键业务操作
- ❌ 不记录敏感信息（密码、Token）

---

## 7. 代码重构优先级指南

### 7.1 API 层重构要点

**Controller 方法返回值规范**:
```csharp
// ❌ 错误：使用 ActionResult 包装
public async Task<ActionResult<PagedResultDto<UserOutputDto>>> GetPageAsync(...)

// ✅ 正确：直接返回对象
public async Task<PagedResultDto<UserOutputDto>> GetPageAsync(...)
```

**Controller 方法命名统一**:
| 当前命名 | 标准命名 | 说明 |
|---------|---------|------|
| `GetListAsync` | `GetPageAsync` | 分页查询 |
| `GetByIdAsync` | `GetAsync` | 单个查询 |
| `CreateAsync` | `CreateAsync` | ✅ 已符合 |
| `UpdateAsync` | `UpdateAsync` | ✅ 已符合 |
| `DeleteAsync` | `DeleteAsync` | ✅ 已符合 |

### 7.2 Application 层重构要点

**移除 EF Core 依赖**:
```csharp
// ❌ 错误：Application 层引用 EF Core
using Microsoft.EntityFrameworkCore;

// ✅ 正确：使用 Leistd 扩展方法
using Leistd.Ddd.Infrastructure.Repositories;
```

**DTO 命名规范化**:
```csharp
// ❌ 错误命名
public record GetUserListInputDto { }
public record UserDto { }

// ✅ 正确命名
public record GetUserPagedInputDto : PagedRequestDto { }
public record UserOutputDto { }
```

**变量命名统一**:
```csharp
// ❌ 错误：参数命名不一致
public async Task CreateAsync(CreateUserInputDto dto, ...)
public async Task UpdateAsync(UpdateUserInputDto request, ...)

// ✅ 正确：统一使用 input
public async Task CreateAsync(CreateUserInputDto input, ...)
public async Task UpdateAsync(UpdateUserInputDto input, ...)
```

### 7.3 Domain 层重构要点

**充血模型改造**:
```csharp
// ❌ 贫血模型
public class User : Entity<Guid>
{
    public string Username { get; set; }
    public bool IsActive { get; set; }
}

// ✅ 充血模型
public class User : Entity<Guid>
{
    public string Username { get; private set; }
    public bool IsActive { get; private set; }

    private User() { }

    public User(string username)
    {
        Id = Guid.NewGuid();
        Username = username;
        IsActive = true;
    }

    public void Enable() => IsActive = true;
    public void Disable() => IsActive = false;
}
```

**规约模式应用**:
```csharp
// 定义规约
public class ActiveUserSpecification : Specification<User>
{
    public override Expression<Func<User, bool>> ToExpression()
        => user => user.IsActive;
}

// 使用规约
var activeUsers = await userRepository.GetListAsync(
    new ActiveUserSpecification(), cancellationToken);
```

### 7.4 .NET 10 新特性应用

**主构造函数**:
```csharp
// ❌ 旧写法
public class UserAppService : IAppService
{
    private readonly IRepository<User, Guid> _userRepository;

    public UserAppService(IRepository<User, Guid> userRepository)
    {
        _userRepository = userRepository;
    }
}

// ✅ 新写法
public class UserAppService(
    IRepository<User, Guid> userRepository) : IAppService
{
    // 直接使用 userRepository
}
```

**集合表达式**:
```csharp
// ❌ 旧写法
var roles = new List<string> { "Admin", "User" };

// ✅ 新写法
var roles = ["Admin", "User"];
```

### 7.5 重构检查清单

#### API 层检查
- [ ] 移除所有 `ActionResult<T>` 包装
- [ ] 统一方法命名（GetPageAsync、GetAsync 等）
- [ ] 使用主构造函数
- [ ] 参数命名统一为 `input`

#### Application 层检查
- [ ] 移除 `using Microsoft.EntityFrameworkCore;`
- [ ] 使用 `using Leistd.Ddd.Infrastructure.Repositories;`
- [ ] DTO 命名符合规范（*InputDto、*OutputDto）
- [ ] 分页查询方法命名为 `GetPagedListAsync`
- [ ] 变量命名统一（input、result、query）

#### Domain 层检查
- [ ] 实体属性使用 `private set`
- [ ] 包含 `private` 无参构造函数
- [ ] 通过公共方法修改状态
- [ ] 无 EF Core 引用
- [ ] 应用规约模式

#### 全局检查
- [ ] 使用文件范围 namespace
- [ ] DTO 使用 record 类型
- [ ] 使用 .NET 10 集合表达式
- [ ] 代码注释完善
- [ ] 单元测试覆盖

---

## 附录：规范检查清单

### ✅ 分层架构检查

- [ ] HTTP 相关处理保留在 API 层
- [ ] Application 层协调业务逻辑
- [ ] Domain 层包含核心业务逻辑
- [ ] Infrastructure 层负责技术实现
- [ ] Domain 层无 EF Core 引用

### ✅ 编码规范检查

- [ ] 使用主构造函数
- [ ] 使用文件范围 namespace
- [ ] DTO 使用 record 类型
- [ ] 异步方法名以 `Async` 结尾
- [ ] 实体使用充血模型

### ✅ 命名规范检查

- [ ] DTO 命名符合规范
- [ ] Dto 参数命名为 `input`
- [ ] 返回对象命名为 `result`
- [ ] 仓储注入命名为 `{entity}Repository`
- [ ] 领域服务命名为 `{Entity}DomainService`

### ✅ Leistd 框架使用检查

- [ ] 优先使用 Leistd 框架已有能力
- [ ] 应用服务继承自 `IAppService`
- [ ] 分页查询使用 `GetPagedListAsync`
- [ ] DTO 映射使用 AutoMapper
- [ ] 使用 Leistd 提供的异步扩展方法

---

**文档版本**: v3.1
**最后更新**: 2026-03-17
**维护者**: 开发团队

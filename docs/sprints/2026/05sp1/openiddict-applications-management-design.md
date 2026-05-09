# 开放应用管理（OpenIddictApplications）设计文档

## 1. 目标

### 1.1 背景

当前系统已经完成 OpenIddict OAuth2/OIDC 授权服务器迁移，并已经具备：

- `/connect/authorize` 授权端点。
- `/connect/token` 换取 token / refresh token 端点。
- `/connect/userinfo` 用户信息端点。
- `/connect/logout` 登出端点。
- OAuth2 Authorization Code + PKCE。
- Refresh Token Flow。
- Cookie 登录态与 Bearer Token API 访问。

桌面端、Web 端或未来第三方客户端接入时，需要在 `OpenIddictApplications` 表中维护 OAuth client 信息。当前 `ai-relay-desktop` 需要手工 SQL 插入，后续应在后台管理 `/platform` 中提供“开放应用管理”页面，供管理员维护 OpenIddict 应用。

### 1.2 建设目标

1. 在后台管理 `/platform` 下新增开放应用管理页面，用于管理 `OpenIddictApplications`。
2. 第一阶段仅实现前端页面与 `_mock` 数据/API，后端接口后续实现。
3. 页面交互、代码分层、DTO 命名、Service 命名、Mock 模式参考现有订阅管理模块。
4. UI 优先使用 PrimeNG v21 官方组件，并使用 Tailwind CSS v4 utility class 完成布局与细节样式。
5. 前端代码遵循 Angular v21 风格：standalone component、signals、`input()`、`output()`、`model()`、`ChangeDetectionStrategy.OnPush`、懒加载路由。
6. 后端设计遵循 .NET 10 / ASP.NET Core Web API 风格：Controller 薄封装、AppService 承载用例、DTO 输入输出分离、异步方法携带 `CancellationToken`。

### 1.3 非目标

第一阶段不实现真实后端持久化接口，不直接修改 OpenIddict 内部表数据。

第一阶段不实现授权记录、Token 记录、Scope 管理页面，仅聚焦 `OpenIddictApplications`。

第一阶段不在管理页暴露 `ClientSecret` 明文二次查看能力。机密客户端创建/重置时后续可单独设计一次性展示机制。

## 2. 现有订阅管理代码规范分析

### 2.1 前端模块结构

订阅管理当前采用按功能模块聚合的结构：

```text
frontend/src/app/features/platform
├── components
│   └── subscriptions
│       ├── subscriptions.ts
│       ├── subscriptions.html
│       └── widgets
│           ├── subscription-table
│           │   ├── subscription-table.ts
│           │   └── subscription-table.html
│           ├── subscription-edit-dialog
│           │   ├── subscription-edit-dialog.ts
│           │   └── subscription-edit-dialog.html
│           └── subscription-metrics-cards
│               ├── subscription-metrics-cards.ts
│               └── subscription-metrics-cards.html
├── models
│   └── subscription.dto.ts
└── services
    ├── subscription-service.ts
    └── subscription-metric-service.ts
```

可复用规范：

- 页面组件负责搜索、筛选、分页状态、弹窗状态和调用 Service。
- `widgets` 子组件负责表格、编辑弹窗、指标卡片等局部 UI。
- DTO 放在 `features/platform/models`。
- HTTP Service 放在 `features/platform/services`，使用 `providedIn: 'root'`。
- 路由在 `platform.routes.ts` 使用 `loadComponent` 懒加载页面组件。

### 2.2 订阅页面组件模式

`subscriptions.ts` 的主要模式：

- 使用 `signal` 管理列表、总数、筛选条件、分页、loading、弹窗状态。
- 使用 `inject()` 注入 Service、`MessageService`、`ConfirmationService`、`LayoutService`。
- 使用 `Subject + debounceTime + distinctUntilChanged` 做搜索防抖。
- 通过 `FilterStateService` 持久化筛选状态。
- 页面初始化时设置标题：`layoutService.title.set('订阅管理')`。
- 页面组件只编排行为，不承载复杂展示逻辑。

开放应用管理应沿用：

```text
OpenApplicationsPage
├── openApplications = signal<OpenApplicationOutputDto[]>([])
├── totalRecords = signal(0)
├── loading = signal(false)
├── editDialogVisible = signal(false)
├── editDialogLoading = signal(false)
├── editDialogSaving = signal(false)
├── selectedApplication = signal<OpenApplicationOutputDto | null>(null)
├── searchQuery = signal('')
├── selectedClientType = signal<OpenApplicationClientType | null>(null)
├── selectedApplicationType = signal<OpenApplicationType | null>(null)
├── offset = signal(0)
├── limit = signal(10)
└── sorting = signal('clientId asc')
```

### 2.3 表格组件模式

`subscription-table` 的主要模式：

- 独立 standalone widget。
- 使用 `input.required<T>()` 接收列表和总数。
- 使用 `output` / `EventEmitter` 向页面组件发送 edit/delete/status/filterChange 事件。
- 使用 PrimeNG `p-table` 的 lazy loading、分页、排序。
- 使用 Tailwind class 控制响应式列显示和 hover 细节。
- 使用 `p-tag`、`p-button`、`p-tooltip`、`p-toggleswitch`、`p-popover` 表达状态和操作。
- 空状态使用 `emptymessage` 模板。

开放应用表格应沿用：

- `p-table [lazy]="true"`。
- `TableLazyLoadEvent` 转换为 `{ offset, limit, sorting }`。
- `currentPageReportTemplate="共 {totalRecords} 条"`。
- `rowsPerPageOptions="[10, 20, 50, 100]"`。
- 行操作放右侧，hover 时显示编辑/删除/重置密钥等动作。
- `clientType`、`applicationType`、`consentType`、PKCE 状态使用 `p-tag`。

### 2.4 编辑弹窗模式

`subscription-edit-dialog` 的主要模式：

- 独立 standalone widget。
- 通过 `visible`、`loading`、`saving`、`subscription` 输入控制弹窗。
- 通过 `visibleChange` 和 `saved` 输出结果。
- 使用 `formModel = signal<FormModel>()` 管理表单数据。
- `@Input() set subscription(...)` 在编辑/新增之间切换并填充表单。
- 使用 `isValid()` 做保存按钮禁用。
- 使用 PrimeNG `p-dialog`、`p-inputText`、`p-textarea`、`p-datepicker`、`p-autoComplete`、`p-tag`。
- 使用 `DialogLoadingComponent` 表示编辑加载中。

开放应用编辑弹窗应沿用：

- `OpenApplicationEditDialogComponent`。
- 新建时允许输入 `clientId`，编辑时禁用 `clientId`。
- `clientType = public` 时不展示/不要求 `clientSecret`。
- `clientType = confidential` 时支持填写 secret 或选择“自动生成/重置”，后端阶段再实现一次性返回。
- `permissions`、`redirectUris`、`postLogoutRedirectUris`、`requirements` 采用分组化表单编辑，避免直接让管理员编辑 JSON 字符串。

### 2.5 Mock API/Data 模式

订阅 mock 当前结构：

```text
frontend/_mock
├── api
│   └── subscription.ts
└── data
    └── subscriptions.ts
```

Mock API 规范：

- API map 使用 `METHOD path`：`'GET /api/v1/api-keys'`。
- CRUD 函数拆分为 `getXxx`、`getXxxById`、`createXxx`、`updateXxx`、`deleteXxx`。
- 使用 `MockRequest` 读取 `params`、`queryParams`、`body`。
- 使用 `MockException` 返回 400/404 等业务错误。
- 分页返回 `PagedResultDto<T>`：`{ totalCount, items }`。
- mock 数据使用可变数组模拟增删改。

开放应用 mock 应新增：

```text
frontend/_mock
├── api
│   └── open-application.ts
└── data
    └── open-applications.ts
```

并在 mock API 总入口中注册 `OPEN_APPLICATION_API`。

## 3. OpenIddictApplications 业务设计

### 3.1 数据来源

当前迁移中 `OpenIddictApplications` 字段：

```text
OpenIddictApplications
├── Id text PK
├── ApplicationType varchar(50)
├── ClientId varchar(100) unique
├── ClientSecret text
├── ClientType varchar(50)
├── ConcurrencyToken varchar(50)
├── ConsentType varchar(50)
├── DisplayName text
├── DisplayNames text
├── JsonWebKeySet text
├── Permissions text
├── PostLogoutRedirectUris text
├── Properties text
├── RedirectUris text
├── Requirements text
└── Settings text
```

OpenIddict 内部通常将 `Permissions`、`RedirectUris`、`PostLogoutRedirectUris`、`Requirements` 等集合字段以 JSON 字符串存储。管理页面不应要求管理员直接填写完整 JSON，而应通过结构化表单生成。

### 3.2 管理对象命名

业务命名建议使用“开放应用”，避免后台页面直接暴露 OpenIddict 内部术语。

前端命名：

```text
OpenApplication
OpenApplicationOutputDto
CreateOpenApplicationInputDto
UpdateOpenApplicationInputDto
GetOpenApplicationsInputDto
OpenApplicationService
OpenApplicationsPage
OpenApplicationTable
OpenApplicationEditDialogComponent
```

后端命名：

```text
OpenApplicationsController
IOpenApplicationAppService
OpenApplicationAppService
OpenApplicationOutputDto
CreateOpenApplicationInputDto
UpdateOpenApplicationInputDto
GetOpenApplicationPagedInputDto
```

### 3.3 管理页面字段设计

#### 3.3.1 列表字段

| 字段 | 来源 | 展示方式 | 说明 |
| --- | --- | --- | --- |
| 应用名称 | `DisplayName` | 主标题 | 为空时显示 `ClientId`。 |
| Client ID | `ClientId` | 等宽文本 + 复制 | OAuth client_id。 |
| 应用类型 | `ApplicationType` | `p-tag` | `web` / `native` / `service`。 |
| 客户端类型 | `ClientType` | `p-tag` | `public` / `confidential`。 |
| 授权确认 | `ConsentType` | `p-tag` | `implicit` / `explicit` / `external` / `systematic`。 |
| 授权能力 | `Permissions` | 标签摘要 | endpoints、grant types、scopes。 |
| 回调地址 | `RedirectUris` | 数量 + popover | 展示前 1 个，剩余 popover。 |
| PKCE | `Requirements` | `p-tag` | 是否包含 `ft:pkce`。 |
| 操作 | - | 按钮 | 编辑、删除、复制 Client ID、重置密钥。 |

#### 3.3.2 表单字段

基础信息：

| 字段 | 类型 | 是否必填 | 说明 |
| --- | --- | --- | --- |
| 应用名称 | string | 否 | `DisplayName`。 |
| Client ID | string | 是 | 创建后不可编辑。 |
| 应用类型 | enum | 是 | `web`、`native`、`service`，默认 `web`。 |
| 客户端类型 | enum | 是 | `public`、`confidential`。 |
| 授权确认类型 | enum | 是 | 默认 `implicit`。 |

回调地址：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| Redirect URIs | string[] | Authorization Code 回调地址。 |
| Post Logout Redirect URIs | string[] | 登出回调地址。 |

权限能力：

| 组 | 值 | 说明 |
| --- | --- | --- |
| Endpoints | `ept:authorization`、`ept:token`、`ept:end_session` | 控制可访问端点。 |
| Grant Types | `gt:authorization_code`、`gt:refresh_token`、`gt:client_credentials` | 控制授权类型。 |
| Response Types | `rst:code` | 授权码流需要。 |
| Scopes | `scp:openid`、`scp:profile`、`scp:email`、`scp:roles`、`scp:offline_access` | 客户端可请求 scope。 |
| Requirements | `ft:pkce` | public/native 推荐强制 PKCE。 |

### 3.4 预设模板

为了降低配置错误概率，新增表单时应提供模板选择。

#### 3.4.1 Web SPA / BFF 客户端

```text
ApplicationType = web
ClientType = public
ConsentType = implicit
Permissions = [
  ept:authorization,
  ept:end_session,
  ept:token,
  gt:authorization_code,
  gt:refresh_token,
  rst:code,
  scp:openid,
  scp:profile,
  scp:email,
  scp:roles,
  scp:offline_access
]
Requirements = [ft:pkce]
```

#### 3.4.2 桌面端客户端

用于 `ai-relay-desktop`：

```text
ApplicationType = native
ClientType = public
ConsentType = implicit
RedirectUris = [ai-relay-desktop://oauth/callback]
PostLogoutRedirectUris = [ai-relay-desktop://oauth/logout-callback]
Permissions = [
  ept:authorization,
  ept:end_session,
  ept:token,
  gt:authorization_code,
  gt:refresh_token,
  rst:code,
  scp:openid,
  scp:profile,
  scp:email,
  scp:roles,
  scp:offline_access
]
Requirements = [ft:pkce]
```

#### 3.4.3 服务端机密客户端

后续如需服务端到服务端调用：

```text
ApplicationType = service
ClientType = confidential
ConsentType = systematic
Permissions = [
  ept:token,
  gt:client_credentials
]
Requirements = []
```

第一阶段 mock 可以展示该模板，但后端阶段需进一步确认是否启用 `client_credentials`。

### 3.5 安全约束

1. `ClientSecret` 不在列表中展示明文。
2. 创建/重置机密客户端 secret 后，后端阶段应采用一次性返回设计。
3. 删除应用前必须二次确认，并提示会影响正在使用该 client 的客户端登录。
4. `ClientId` 创建后不可编辑，避免破坏已有客户端配置。
5. `public` 客户端应默认强制 PKCE。
6. `native` 客户端应默认使用自定义协议或 loopback redirect uri，不建议使用普通网页 redirect uri。
7. `offline_access` 与 `gt:refresh_token` 应联动提示：允许 refresh token 时通常需要开放 `scp:offline_access`。
8. 第一阶段只做 UI 与 mock，不应引入绕过 OpenIddict 校验的真实数据库直写逻辑。

## 4. 核心设计

### 4.1 第一阶段前端目录结构

```text
frontend/src/app/features/platform
├── components
│   └── open-applications
│       ├── open-applications.ts
│       ├── open-applications.html
│       └── widgets
│           ├── open-application-table
│           │   ├── open-application-table.ts
│           │   └── open-application-table.html
│           ├── open-application-edit-dialog
│           │   ├── open-application-edit-dialog.ts
│           │   └── open-application-edit-dialog.html
│           └── open-application-permission-summary
│               ├── open-application-permission-summary.ts
│               └── open-application-permission-summary.html
├── models
│   └── open-application.dto.ts
└── services
    └── open-application-service.ts
```

```text
frontend/_mock
├── api
│   └── open-application.ts
└── data
    └── open-applications.ts
```

路由调整：

```text
frontend/src/app/features/platform/platform.routes.ts
└── path: 'open-applications'
    loadComponent: () => import('./components/open-applications/open-applications').then(m => m.OpenApplicationsPage)
```

如果侧边栏菜单存在集中配置，后续还需要新增：

```text
开放应用管理 -> /platform/open-applications
```

### 4.2 前端 DTO 设计

`open-application.dto.ts`：

```typescript
import { PagedRequestDto } from '../../../shared/models/paged-request.dto';

export type OpenApplicationType = 'web' | 'native' | 'service';
export type OpenApplicationClientType = 'public' | 'confidential';
export type OpenApplicationConsentType = 'implicit' | 'explicit' | 'external' | 'systematic';

export interface OpenApplicationOutputDto {
  id: string;
  clientId: string;
  displayName?: string;
  applicationType?: OpenApplicationType;
  clientType?: OpenApplicationClientType;
  consentType?: OpenApplicationConsentType;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  permissions: string[];
  requirements: string[];
  settings: Record<string, unknown>;
  properties: Record<string, unknown>;
  hasClientSecret: boolean;
}

export interface CreateOpenApplicationInputDto {
  clientId: string;
  displayName?: string;
  applicationType: OpenApplicationType;
  clientType: OpenApplicationClientType;
  clientSecret?: string;
  consentType: OpenApplicationConsentType;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  permissions: string[];
  requirements: string[];
}

export interface UpdateOpenApplicationInputDto {
  displayName?: string;
  applicationType: OpenApplicationType;
  clientType: OpenApplicationClientType;
  consentType: OpenApplicationConsentType;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  permissions: string[];
  requirements: string[];
}

export interface ResetOpenApplicationSecretOutputDto {
  clientSecret: string;
}

export interface GetOpenApplicationsInputDto extends PagedRequestDto {
  keyword?: string;
  applicationType?: OpenApplicationType;
  clientType?: OpenApplicationClientType;
}
```

说明：

- 前端 DTO 不暴露 OpenIddict 原始 JSON 字符串，统一转为数组/对象。
- `hasClientSecret` 用于展示是否为机密客户端或是否已有 secret，不传输明文 secret。
- `ResetOpenApplicationSecretOutputDto` 留给后端阶段实现一次性展示。

### 4.3 前端 Service 设计

`open-application-service.ts`：

```typescript
@Injectable({ providedIn: 'root' })
export class OpenApplicationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/open-applications';

  getOpenApplications(input?: GetOpenApplicationsInputDto): Observable<PagedResultDto<OpenApplicationOutputDto>>;

  getOpenApplication(id: string): Observable<OpenApplicationOutputDto>;

  createOpenApplication(data: CreateOpenApplicationInputDto): Observable<OpenApplicationOutputDto>;

  updateOpenApplication(id: string, data: UpdateOpenApplicationInputDto): Observable<OpenApplicationOutputDto>;

  deleteOpenApplication(id: string): Observable<void>;

  resetSecret(id: string): Observable<ResetOpenApplicationSecretOutputDto>;
}
```

接口路径第一阶段 mock 先按后端目标设计：

```http
GET    /api/v1/open-applications
GET    /api/v1/open-applications/{id}
POST   /api/v1/open-applications
PUT    /api/v1/open-applications/{id}
DELETE /api/v1/open-applications/{id}
POST   /api/v1/open-applications/{id}/reset-secret
```

### 4.4 页面组件设计

`OpenApplicationsPage` 职责：

1. 设置布局标题：`开放应用管理`。
2. 管理筛选状态：关键字、应用类型、客户端类型。
3. 管理分页排序状态。
4. 调用 `OpenApplicationService` 加载列表和详情。
5. 打开新增/编辑弹窗。
6. 删除应用前调用 `ConfirmationService.confirm`。
7. 重置密钥前二次确认，并在成功后弹窗展示一次性 secret。

页面筛选区：

- `p-select`：应用类型筛选。
- `p-select`：客户端类型筛选。
- `p-iconfield + p-inputicon + pInputText`：关键字搜索。
- `p-button`：刷新。
- `p-button`：新建开放应用。

Tailwind 布局沿用订阅页面：

```html
<div class="flex flex-col h-full overflow-hidden">
  <div class="flex-1 overflow-y-auto p-6 custom-scrollbar">
    <div class="flex flex-col md:flex-row justify-between items-stretch md:items-center gap-4 mb-6">
      ...filters...
      ...actions...
    </div>
    <app-open-application-table ... />
  </div>
</div>
```

### 4.5 表格组件设计

`OpenApplicationTable` 输入输出：

```typescript
export interface OpenApplicationTableFilterEvent {
  offset: number;
  limit: number;
  sorting?: string;
}

@Component({
  selector: 'app-open-application-table',
  imports: [...],
  templateUrl: './open-application-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OpenApplicationTable {
  applications = input.required<OpenApplicationOutputDto[]>();
  totalRecords = input.required<number>();
  loading = input<boolean>(false);

  edit = output<string>();
  delete = output<string>();
  resetSecret = output<string>();
  filterChange = output<OpenApplicationTableFilterEvent>();
}
```

建议列：

```text
应用名称 / Client ID
类型
授权能力
回调地址
PKCE
Secret
操作
```

列设计：

- 应用名称列：主显示 `displayName || clientId`，副显示 `clientId`，提供复制按钮。
- 类型列：展示 `applicationType`、`clientType` 两个 tag。
- 授权能力列：展示 grant type / scope 摘要，更多使用 `p-popover`。
- 回调地址列：展示第一个 redirect uri，更多使用 `p-popover`。
- PKCE 列：`requirements.includes('ft:pkce')` 显示启用/未启用。
- Secret 列：只显示 `public` 或 `已设置`，不显示明文。
- 操作列：编辑、重置密钥、删除。

PrimeNG 组件：

- `TableModule`
- `ButtonModule`
- `TagModule`
- `TooltipModule`
- `PopoverModule`
- `ConfirmPopupModule` 或页面级 `ConfirmDialogModule`

### 4.6 编辑弹窗设计

`OpenApplicationEditDialogComponent` 输入输出：

```typescript
@Component({
  selector: 'app-open-application-edit-dialog',
  imports: [...],
  templateUrl: './open-application-edit-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OpenApplicationEditDialogComponent {
  visible = model(false);
  loading = input(false);
  saving = input(false);
  application = input<OpenApplicationOutputDto | null>(null);
  saved = output<CreateOpenApplicationInputDto | UpdateOpenApplicationInputDto>();
}
```

说明：

- Angular v21 推荐使用 `input()`、`output()`、`model()`；订阅模块当前部分仍使用 `@Input/@Output`，新模块可以在不破坏风格的前提下采用新 API。
- 表单状态使用 `signal<OpenApplicationEditFormModel>`。
- 派生状态使用 `computed()`，如 `isEditMode`、`isPublicClient`、`isNativeClient`、`isValid`。

表单分区：

```text
基础信息
├── 应用模板
├── 应用名称
├── Client ID
├── 应用类型
├── 客户端类型
└── 授权确认类型

回调地址
├── Redirect URIs
└── Post Logout Redirect URIs

授权能力
├── Endpoints
├── Grant Types
├── Response Types
└── Scopes

安全要求
└── PKCE
```

URI 数组编辑方式：

- 第一阶段优先使用简单、可维护的“输入框 + 添加按钮 + tag 列表 + 删除按钮”。
- 后续如 PrimeNG v21 `p-chips`/`p-autoComplete multiple` 在项目版本中表现稳定，可再替换。

权限多选：

- 使用 `p-multiselect` 或分组 checkbox。
- 管理体验上推荐按组展示 checkbox，而不是一个很长的 multiselect。
- 保存时将所有选中项合并为 `permissions: string[]`。

模板联动：

- 选择“桌面端客户端”模板后，自动填充 `applicationType=native`、`clientType=public`、`requirements=[ft:pkce]`、常用 permissions。
- 自动填充示例 redirect uri，但允许编辑。
- 如果用户切换为 `public`，自动清空 `clientSecret`。
- 如果用户勾选 `gt:refresh_token`，提示建议同时勾选 `scp:offline_access`。

### 4.7 Mock 设计

`frontend/_mock/data/open-applications.ts`：

```typescript
export const OPEN_APPLICATIONS: MockOpenApplication[] = [
  {
    id: 'ai-relay-desktop',
    clientId: 'ai-relay-desktop',
    displayName: 'AiRelay Desktop',
    applicationType: 'native',
    clientType: 'public',
    consentType: 'implicit',
    redirectUris: ['ai-relay-desktop://oauth/callback'],
    postLogoutRedirectUris: ['ai-relay-desktop://oauth/logout-callback'],
    permissions: [
      'ept:authorization',
      'ept:end_session',
      'ept:token',
      'gt:authorization_code',
      'gt:refresh_token',
      'rst:code',
      'scp:openid',
      'scp:profile',
      'scp:email',
      'scp:roles',
      'scp:offline_access'
    ],
    requirements: ['ft:pkce'],
    settings: {},
    properties: {},
    hasClientSecret: false
  }
];
```

`frontend/_mock/api/open-application.ts`：

```typescript
export const OPEN_APPLICATION_API = {
  'GET /api/v1/open-applications': (req: MockRequest) => getOpenApplications(req),
  'GET /api/v1/open-applications/:id': (req: MockRequest) => getOpenApplication(req),
  'POST /api/v1/open-applications': (req: MockRequest) => createOpenApplication(req),
  'PUT /api/v1/open-applications/:id': (req: MockRequest) => updateOpenApplication(req),
  'DELETE /api/v1/open-applications/:id': (req: MockRequest) => deleteOpenApplication(req),
  'POST /api/v1/open-applications/:id/reset-secret': (req: MockRequest) => resetOpenApplicationSecret(req)
};
```

Mock 行为：

- `clientId` 唯一校验，重复返回 400。
- `clientId` 不存在返回 404。
- `public` 客户端不允许保存 `clientSecret`。
- `native/public` 默认要求 `ft:pkce`。
- `reset-secret` 仅允许 `confidential` 客户端，返回一次性 mock secret。
- 搜索支持 `clientId`、`displayName`。
- 筛选支持 `applicationType`、`clientType`。

### 4.8 后端目标目录结构

第二阶段后端实现建议结构：

```text
backend/src
├── AiRelay.Api
│   └── Controllers
│       └── OpenApplicationsController.cs
├── AiRelay.Application
│   └── OpenApplications
│       ├── AppServices
│       │   ├── IOpenApplicationAppService.cs
│       │   └── OpenApplicationAppService.cs
│       └── Dtos
│           ├── OpenApplicationOutputDto.cs
│           ├── CreateOpenApplicationInputDto.cs
│           ├── UpdateOpenApplicationInputDto.cs
│           ├── GetOpenApplicationPagedInputDto.cs
│           └── ResetOpenApplicationSecretOutputDto.cs
└── AiRelay.Infrastructure
    └── OpenIddict
        └── OpenIddictApplicationStoreAdapter.cs  # 如需封装 OpenIddict manager 查询/分页
```

后端优先使用 OpenIddict 官方 manager，而不是直接操作 EF Core 表：

```text
IOpenIddictApplicationManager
```

原因：

- OpenIddict manager 负责 descriptor 与存储格式转换。
- 避免自行拼接内部 JSON 字符串导致格式不兼容。
- 避免绕过 OpenIddict 对 client secret、permissions、requirements 的规范处理。

### 4.9 后端 API 设计

```http
GET /api/v1/open-applications?keyword=&applicationType=&clientType=&offset=0&limit=10&sorting=clientId asc
GET /api/v1/open-applications/{id}
POST /api/v1/open-applications
PUT /api/v1/open-applications/{id}
DELETE /api/v1/open-applications/{id}
POST /api/v1/open-applications/{id}/reset-secret
```

Controller：

```csharp
[Authorize]
[Route("api/v1/open-applications")]
public class OpenApplicationsController(IOpenApplicationAppService appService) : BaseController
{
    [HttpGet]
    public Task<PagedResultDto<OpenApplicationOutputDto>> GetPagedListAsync(
        [FromQuery] GetOpenApplicationPagedInputDto input,
        CancellationToken cancellationToken)
        => appService.GetPagedListAsync(input, cancellationToken);
}
```

AppService：

```csharp
public interface IOpenApplicationAppService : IAppService
{
    Task<PagedResultDto<OpenApplicationOutputDto>> GetPagedListAsync(GetOpenApplicationPagedInputDto input, CancellationToken cancellationToken = default);
    Task<OpenApplicationOutputDto> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<OpenApplicationOutputDto> CreateAsync(CreateOpenApplicationInputDto input, CancellationToken cancellationToken = default);
    Task<OpenApplicationOutputDto> UpdateAsync(string id, UpdateOpenApplicationInputDto input, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<ResetOpenApplicationSecretOutputDto> ResetSecretAsync(string id, CancellationToken cancellationToken = default);
}
```

### 4.10 PrimeNG / Angular / .NET 指南落地

#### PrimeNG

优先使用：

- `p-table`：列表、分页、排序、空状态。
- `p-dialog`：新增/编辑弹窗。
- `p-button`：页面和行操作。
- `p-select`：枚举筛选和单选。
- `p-multiselect` 或 checkbox 组：权限多选。
- `p-tag`：类型、权限、安全要求状态。
- `p-tooltip`：字段解释和按钮提示。
- `p-popover`：展示 redirect uri / permission 详情。
- `p-confirmDialog` / `p-confirmPopup`：危险操作确认。
- `p-toast` 通过全局 `MessageService` 显示结果。

#### Angular v21

新模块应尽量使用：

- standalone components。
- `ChangeDetectionStrategy.OnPush`。
- `inject()` 注入服务。
- `signal()` 管理组件状态。
- `computed()` 管理派生状态。
- `input()`、`output()`、`model()` 替代新增组件中的 `@Input/@Output`。
- `@if`、`@for`、`@switch` 原生控制流。
- 严格类型，避免 `any`。
- Feature route 使用 `loadComponent` 懒加载。

考虑到项目现有订阅模块仍使用 `FormsModule + ngModel` 管理弹窗表单，第一阶段可沿用该模式保持一致；但新增组件 API 使用 Angular 新式 `input/output/model`。

#### .NET 10

后端阶段应遵循：

- Controller 只做 HTTP 参数绑定和调用 AppService。
- AppService 负责用例编排、DTO 转换和业务校验。
- DTO 输入输出分离。
- 所有异步方法保留 `CancellationToken cancellationToken = default`。
- 使用 `[Authorize]` 和后续管理员权限策略保护管理接口。
- 优先使用 OpenIddict manager，而不是直接操作 OpenIddict EF 表。
- 对 `ClientId` 唯一性、URI 格式、public/confidential 约束做服务端校验。

## 5. 实施计划

### 阶段一：前端与 `_mock` 实现

#### 5.1.1 DTO 与 Service

1. 新增 `frontend/src/app/features/platform/models/open-application.dto.ts`。
2. 新增 `frontend/src/app/features/platform/services/open-application-service.ts`。
3. Service 实现分页查询、详情、创建、更新、删除、重置密钥方法。
4. DTO 与 Service 命名保持 `OpenApplication` 前缀，不直接使用 `OpenIddictApplication` 暴露内部实现名。

#### 5.1.2 Mock Data 与 Mock API

1. 新增 `frontend/_mock/data/open-applications.ts`。
2. 新增 `frontend/_mock/api/open-application.ts`。
3. 在 mock API 聚合入口注册 `OPEN_APPLICATION_API`。
4. Mock 数据至少包含：
   - `ai-relay-desktop` native public PKCE 客户端。
   - `ai-relay-web` web public PKCE 客户端。
   - 一个 confidential service 示例客户端。
5. Mock API 实现分页、搜索、筛选、创建、更新、删除、重置密钥。
6. Mock API 做基础校验：clientId 唯一、public 不允许 secret、confidential 可重置 secret。

#### 5.1.3 页面与路由

1. 新增 `open-applications` 页面组件。
2. 在 `platform.routes.ts` 增加 `/platform/open-applications` 路由。
3. 如侧边栏菜单有集中配置，增加“开放应用管理”菜单项。
4. 页面初始化设置标题为“开放应用管理”。
5. 页面筛选状态支持关键字、应用类型、客户端类型。

#### 5.1.4 表格组件

1. 新增 `open-application-table`。
2. 使用 PrimeNG `p-table` lazy 分页排序。
3. 实现 Client ID 复制、permissions popover、redirect uri popover。
4. 实现编辑、删除、重置密钥操作事件。
5. 实现空状态。

#### 5.1.5 编辑弹窗

1. 新增 `open-application-edit-dialog`。
2. 实现新增/编辑共用表单。
3. 实现模板选择：Web、Desktop、Service。
4. 实现 URI 数组编辑。
5. 实现 permissions / requirements 分组选择。
6. 实现 public/confidential 联动。
7. 实现表单校验和保存按钮禁用。

#### 5.1.6 验证

1. 前端构建通过。
2. 页面可进入 `/platform/open-applications`。
3. Mock 下可完成新增、编辑、删除、重置密钥。
4. 搜索、筛选、分页、排序可用。
5. 桌面端模板生成的 `ai-relay-desktop` 配置与当前 OAuth 设计一致。

### 阶段二：后端 API 设计与实现

1. 新增 `OpenApplicationsController`。
2. 新增 `IOpenApplicationAppService` / `OpenApplicationAppService`。
3. 新增后端 DTO。
4. 使用 `IOpenIddictApplicationManager` 实现创建、更新、删除、查询。
5. 实现 OpenIddict descriptor 与业务 DTO 的双向转换。
6. 实现分页查询。如果 OpenIddict manager 无法高效分页，可增加受控 Adapter，但不直接散落 EF 表操作。
7. 实现 client secret 一次性创建/重置返回。
8. 增加管理员权限策略保护接口。
9. 后端构建通过。

### 阶段三：联调替换 Mock

1. 前端 Service 保持 API 路径不变。
2. 关闭 `_mock` 对 `open-applications` 的拦截。
3. 与真实后端联调分页、详情、创建、更新、删除、重置密钥。
4. 验证新增 `ai-relay-desktop` 后，桌面端 Authorization Code + PKCE 可正常登录。
5. 验证删除/修改 redirect uri 后，OpenIddict 行为符合预期。

## 6. 风险与注意事项

### 6.1 OpenIddict 内部存储格式风险

`OpenIddictApplications` 中多个字段是 JSON 字符串，直接操作数据库容易写出 OpenIddict 不认可的格式。后端阶段应优先使用 OpenIddict manager。

### 6.2 Client Secret 展示风险

机密客户端 secret 应只在创建或重置时一次性展示。列表和详情页只显示 `hasClientSecret`。

### 6.3 权限组合风险

错误组合可能导致授权端点报错。例如：

- 授权码流需要 `gt:authorization_code`、`rst:code`、`ept:authorization`、`ept:token`。
- refresh token 需要 `gt:refresh_token`，客户端请求时通常还需要允许 `scp:offline_access`。
- native/public 客户端应强制 `ft:pkce`。

### 6.4 删除应用影响面

删除应用会影响已配置该 `client_id` 的桌面端/Web 端登录。删除前必须二次确认，后端阶段可考虑检查是否存在相关 authorization/token。

### 6.5 前后端命名一致性

前端使用 `OpenApplication` 作为业务名，后端也应使用同名 AppService/DTO，避免页面层暴露 `OpenIddict` 内部实现细节。

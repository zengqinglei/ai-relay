# 📝 API 文档规范

所有 API 文档应存放在 `docs/services/` 目录下，并遵循以下格式。

#### 获取用户列表

查询并返回一个分页的用户列表，支持通过状态进行过滤。

``` text
GET /api/v1/users
```

**参数**
| 参数名 | Query/Body | 类型 | 必填 | 默认值 | 说明 | 示例值 |
|---|---|---|---|---|---|---|
| `offset` | `query` | `number` | 否 | 0 | 偏移量（默认：0） | `10` |
| `limit` | `query` | `number` | 否 | 10 | 每页数量（默认：10）| `20` |
| `status` | `query` | `string` | 否 |  | 用户状态 (`active`, `inactive`) | `active` |

**示例**

``` bash
curl --location --request GET 'http://${service-url}/user-service/api/v1/users?offset=10&limit=20&status=active' \
--header 'Authorization: Bearer ${token}'
```

**响应**

*Content-Type：* application/json

*成功响应：* HTTP 200 OK
```json
{
  "items": [
    {
      "userId": "usr_12345",
      "username": "admin",
      "email": "admin@example.com",
      "status": "active"
    }
  ],
  "total": 1
}
```

| 字段名 | 类型 | 说明 |
|---|---|---|
| `items` | `UserOutput[]` | 用户信息列表 |
| `items.userId` | `string` | 用户唯一标识 |
| `items.username`| `string` | 用户名 |
| `items.email` | `string` | 用户邮箱 |
| `items.status` | `string` | 用户状态 (`active`, `inactive`) |
| `total` | `number` | 符合条件的总用户数 |

*失败响应：*  HTTP 400 Bad Request
```json
{
  "code": 40001,
  "message": "无效的参数：status"
}
```

| 字段名 | 类型 | 说明 |
|---|---|---|
| `code` | `number` | 业务错误码 |
| `message`| `string` | 错误描述信息 |
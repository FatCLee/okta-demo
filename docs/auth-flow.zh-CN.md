# 认证与邀请流程

English version: [auth-flow.md](auth-flow.md)

这个项目把身份、业务授权和用户体验分开处理：

- Frontend 负责浏览器中的用户体验。
- Backend 负责业务规则、授权和受保护数据。
- Okta 和 Microsoft Entra ID 负责证明身份。

## 用户群体

| 用户 | 身份提供商 | 应用角色 |
| --- | --- | --- |
| Relationship Manager | Microsoft Entra ID | 内部用户，负责创建 KYC 案件和客户邀请 |
| Client | Okta | 外部用户，负责激活/登录并完成 KYC 工作 |

## Frontend 职责

Frontend 负责协调浏览器流程，但不决定某个人到底是谁，也不决定他们能做什么。

Frontend 负责：

- 展示 RM/Client 的登录选择。
- 将 Client 重定向到 Okta。
- 将 RM 重定向到 Microsoft Entra ID。
- 处理 callback URL，例如 `/login/callback` 和 `/rm/callback`。
- 通过 Okta 和 MSAL SDK 接收 access token。
- 使用 `Authorization: Bearer <token>` 把 access token 发送给 backend。
- 根据 backend 响应展示 UI。

Frontend 可以表达：“我有一个来自 Entra ID 的 token，请创建这个邀请。”

Frontend 不应该表达：“相信我，我是 RM。”

## Backend 职责

Backend 做信任判断，并拥有 KYC 业务模型。

Backend 负责：

- 为 Client API 验证 Okta access token。
- 为 RM API 验证 Microsoft Entra ID access token。
- 执行基于角色的访问控制。
- 创建 KYC 案件。
- 创建本地占位客户记录。
- 创建并保存邀请记录。
- 调用 Okta Management API 创建和激活用户。
- 向 frontend 返回案件、邀请和 session 数据。
- 持久化用户、角色、身份提供商链接、案件和邀请。

例如，`POST /api/cases/invite` 只有在 backend 收到有效的 Entra access token 时才会工作。Frontend 发送 token；backend 验证并授权。

## Client 登录流程

这是通过 Okta 进行的外部用户流程。

1. RM 创建 KYC 案件和客户邀请。
2. Backend 创建本地 Client、Case 和 Invite 记录。
3. Backend 可能调用 Okta 创建 staged Okta user。
4. Okta 返回 activation URL/token。
5. Client 收到或打开激活链接。
6. Client 通过 Okta 激活或登录。
7. Frontend 在 `/login/callback` 处理 Okta callback。
8. Frontend 收到 Okta access token。
9. Frontend 用这个 token 调用 backend 的 Client API。
10. Backend 根据 Okta 验证 token。
11. Backend 将 Okta identity 链接到本地 Client/Case 上下文。
12. Client 继续完成 KYC 工作。

## RM 登录流程

这是通过 Microsoft Entra ID 进行的内部用户流程。

1. RM 点击 “Continue with Entra ID”。
2. Frontend 通过 MSAL 重定向到 Microsoft Entra ID。
3. RM 使用企业身份登录。
4. Entra 重定向回 `/rm/callback`。
5. Frontend 收到 Entra access token。
6. Frontend 调用 RM API，例如 `GET /api/rm/session` 或 `POST /api/cases/invite`。
7. Backend 根据 Entra ID 验证 token。
8. Backend 确认用户被授权为 RM。
9. Backend 允许 RM-only 操作。

## Token 如何传给 Backend

Okta 或 Entra 登录之后，frontend 不会把整个登录响应对象转发给 backend。Auth SDK 会处理 redirect callback、保存 token，并把 access token 提供给 frontend。

Frontend 使用 HTTP header 将 access token 发送给 backend：

```http
Authorization: Bearer <access_token>
```

Backend 验证原始 access token。Backend 不应该为了授权而信任 frontend 解码出来的 profile 数据。

### Okta Token 传递

Okta 将浏览器重定向回：

```text
/login/callback?code=...
```

Frontend 处理 callback 并取得 access token：

```ts
await oktaAuth.handleRedirect();

const isAuthenticated = await oktaAuth.isAuthenticated();
const userInfo = await oktaAuth.getUser();
const accessToken = await oktaAuth.tokenManager.get("accessToken");
```

然后 frontend 调用 backend：

```ts
await fetch("/api/client/session", {
  headers: {
    Authorization: `Bearer ${accessToken.accessToken}`,
  },
});
```

解码后的 Okta access token payload 可能长这样：

```json
{
  "ver": 1,
  "jti": "AT.xxxxx",
  "iss": "https://your-domain.okta.com/oauth2/default",
  "aud": "api://default",
  "sub": "00uabc123",
  "cid": "your-okta-client-id",
  "uid": "00uabc123",
  "scp": ["openid", "profile", "email"],
  "auth_time": 1710000000,
  "exp": 1710003600,
  "iat": 1710000000
}
```

如果 Okta groups 被配置为 access-token claim，token 也可能包含：

```json
{
  "groups": ["KYC_Client"]
}
```

### Entra Token 传递

Microsoft Entra ID 将浏览器重定向回：

```text
/rm/callback?code=...
```

Frontend 处理 callback 并取得 access token：

```ts
await msalInstance.initialize();

const redirectResult = await msalInstance.handleRedirectPromise();
const account = redirectResult?.account ?? msalInstance.getAllAccounts()[0];

const tokenResult = await msalInstance.acquireTokenSilent({
  account,
  scopes: getEntraApiScopes(),
});
```

然后 frontend 调用 backend：

```ts
await fetch("/api/rm/session", {
  headers: {
    Authorization: `Bearer ${tokenResult.accessToken}`,
  },
});
```

RM 创建邀请也使用同样的模式：

```ts
await fetch("/api/cases/invite", {
  method: "POST",
  headers: {
    Authorization: `Bearer ${tokenResult.accessToken}`,
    "Content-Type": "application/json",
  },
  body: JSON.stringify(inviteForm),
});
```

解码后的 Entra access token payload 可能长这样：

```json
{
  "aud": "api://your-api-application-client-id",
  "iss": "https://login.microsoftonline.com/<tenant-id>/v2.0",
  "iat": 1710000000,
  "nbf": 1710000000,
  "exp": 1710003600,
  "azp": "your-spa-client-id",
  "name": "Jamie RM",
  "oid": "00000000-0000-0000-0000-000000000000",
  "preferred_username": "jamie@company.com",
  "scp": "access_as_user",
  "sub": "...",
  "tid": "<tenant-id>",
  "ver": "2.0"
}
```

### Backend 验证

对于两个身份提供商，backend 都会验证：

- Token signature。
- Issuer。
- Audience。
- Expiration。
- 必要时验证 scopes、groups 或 claims。
- 本地用户、角色、案件和邀请映射。

Frontend 可以使用解码/profile 信息做展示，例如 “Hello, Jamie”。Backend 授权使用原始 bearer token 和本地业务数据。

## 邀请如何工作

Invitation 有两个部分一起工作：

```text
App invitation = 业务流程
Okta activation = 身份账号设置
```

Okta 不知道什么是 KYC 案件。Backend 知道。

### Frontend 角色

Frontend 是 RM 的操作界面。

对于 RM 创建邀请，frontend 会：

1. 让 RM 使用 Microsoft Entra ID 登录。
2. 从 MSAL 获取 Entra access token。
3. 收集客户邮箱、客户姓名、是否已有账号，以及 RM 名称。
4. 携带 Entra token 调用 backend 邀请端点。

示例请求：

```http
POST /api/cases/invite
Authorization: Bearer <entra_access_token>
Content-Type: application/json
```

示例 body：

```json
{
  "clientEmail": "client@example.com",
  "clientName": "Client Name",
  "hasExistingAccount": false,
  "relationshipManager": "Jamie RM"
}
```

Frontend 不创建 Okta 用户。Frontend 不决定 RM 是否允许创建邀请。Frontend 只把 RM token 和邀请详情发送给 backend。

### Backend 角色

Backend 拥有真正的邀请。

对于邀请创建，backend 会：

1. 验证 Entra access token。
2. 确认调用者允许创建邀请。
3. 创建或查找 Client 的本地 `ApplicationUser`。
4. 创建或查找 RM 的本地 `ApplicationUser`。
5. 创建 `KycCase`。
6. 创建 `ClientInvite`。
7. 如果 Client 是新用户且 Okta Management API 已配置，则调用 Okta。
8. 将 Okta 激活信息存到邀请记录上。
9. 将邀请详情返回给 frontend。

Backend 在本地记录中保存业务含义：

```text
ApplicationUser
UserRole
UserRoleAssignment
IdentityProviderAccount
KycCase
ClientInvite
```

这让 backend 可以表达这样的事实：

```text
Jamie RM 为 KYC case 123 邀请了 client@example.com。
这个邀请处于 pending、sent、redeemed 或 activation-ready 状态。
这个邀请属于 Okta user 00uabc123。
```

### Okta 角色

Okta 拥有外部身份账号生命周期。

对于新 Client，backend 会调用 Okta：

1. 使用 `activate=false` 创建 Okta user。
2. 调用 Okta lifecycle activation endpoint。
3. 收到 `activationUrl`。

Okta 知道：

```text
这个 Okta user 存在。
这个 user 需要激活。
这个激活链接有效。
这个 user 可以设置密码、注册 factor 并登录。
```

Okta 不知道：

```text
这个邀请属于 KYC case 123。
这个 RM 创建了该邀请。
这个 Client 应该看到某个特定的银行业务流程。
```

### 新 Client 流程

对于新 Client：

1. RM 使用 frontend 提交邀请详情。
2. Frontend 携带 Entra access token 调用 `POST /api/cases/invite`。
3. Backend 验证 Entra token。
4. Backend 创建本地 client、case 和 invite 记录。
5. Backend 创建 staged Okta user。
6. Backend 请求 Okta activation link。
7. Okta 返回 `activationUrl`。
8. Backend 将 `activationUrl` 保存到 `ClientInvite`。
9. Backend 将邀请详情返回给 frontend。
10. Frontend 展示或发送激活链接。
11. Client 打开 Okta 激活链接。
12. Okta 激活账号。
13. Client 通过 Okta 登录。
14. Frontend 收到 Okta token。
15. Backend 验证 Okta token，并将用户映射到 case。

### 已有 Client 流程

对于已有 Okta 账号的 Client：

1. RM 使用 frontend 提交邀请详情。
2. Frontend 携带 Entra access token 调用 `POST /api/cases/invite`。
3. Backend 验证 Entra token。
4. Backend 创建本地 case 和 invite 记录。
5. Backend 不需要创建新的 Okta user。
6. Backend 将邀请详情返回给 frontend。
7. Client 使用已有 Okta 账号登录。
8. Frontend 收到 Okta token。
9. Backend 验证 token，并将用户映射到 case/invite。

核心规则是：

```text
Frontend: 收集邀请详情并展示下一步
Backend: 创建、授权并保存业务邀请
Okta: 创建、激活并登录外部身份
```

## Invitation 与 Activation 的区别

Invitation 是应用里的业务概念。Activation 是 Okta 的身份生命周期。

应用拥有：

- 哪个 RM 邀请了哪个 Client。
- 邀请属于哪个 KYC case。
- Client 是否已有账号。
- 邀请状态、审计记录和 case 关联。
- 如果使用自定义邮件，则拥有银行品牌邮件内容。

Okta 拥有：

- Okta user record。
- 账号激活。
- Activation URL/token。
- 激活后的 hosted sign-in。

对于这个 demo，推荐模型是：应用管理 invitation，Okta 管理 activation。Backend 存储 invite record，并调用 Okta 创建/激活用户。Activation link 可以由应用发送；如果需要，也可以使用 Okta 内置的 activation email flow。

## 密码与账号管理

密码修改留在身份提供商中处理。Frontend 可以提供指向 provider-owned account pages 的链接，但 backend 不应该接收旧密码或新密码。

对于 Client，app 链接到 Okta account settings：

```text
https://<okta-org-url>/account-settings/home
```

Frontend 从 `VITE_OKTA_ISSUER` 推导 `<okta-org-url>`。

对于 RM，app 链接到 Microsoft My Sign-Ins 的密码修改页面：

```text
https://mysignins.microsoft.com/security-info/password/change?tileType=ChangePassword
```

这些链接只是 frontend-only navigation。它们不会调用 backend API。

## 心智模型

身份提供商负责认证这个人：

```text
This is alice@company.com.
```

Backend 负责授权这个动作：

```text
Alice is an RM, so she can create a client invite.
```

Frontend 负责促进这个体验：

```text
Alice clicked create invite, so send the request with her token.
```

Okta 和 Entra 证明身份。Backend 将这些身份连接到银行 KYC 业务模型。

# Okta Demo

English version: [README.md](README.md)

银行 KYC 身份提供商演示项目的初始实现：

- `web/`：React + Vite 前端，展示 RM 邀请流程，以及 Client 登录/兑换流程
- `api/`：ASP.NET Core API，包含 SQLite 持久化、EF Core 实体，以及 Okta 和 Entra ID 的真实 token 验证

关于 frontend、backend、Okta 和 Entra ID 的职责划分，请查看 [认证与邀请流程](docs/auth-flow.zh-CN.md)。

## 运行 API

```bash
cd api
dotnet run
```

API 会暴露：

- `GET /api/cases`
- `POST /api/cases/invite`
- `POST /api/auth/login`
- `POST /api/auth/redeem-invite`
- `GET /api/client/session`
- `GET /api/rm/session`

## 运行 Web App

```bash
cd web
npm install
cp .env.example .env
npm run dev
```

Vite dev server 会把 `/api` 代理到 `http://localhost:5204`。

## 持久化模型

API 使用 EF Core + SQLite 做本地演示持久化。API 启动时会自动创建数据库：`api/app_data/okta-demo.db`。

具体实体类位于 `api/Data/Entities`：

- `ApplicationUser`：本地应用用户记录，由 Client 和 RM 共用
- `UserRole`：角色目录，目前包括 `client` 和 `rm`
- `UserRoleAssignment`：本地用户和角色的映射
- `IdentityProviderAccount`：Okta 或 Microsoft Entra ID 账号链接
- `KycCase`：银行 KYC 案件，由 RM 拥有并分配给 Client
- `ClientInvite`：邀请、激活 URL，以及客户 onboarding 流程的兑换状态

`DatabaseSeeder` 会在首次启动时创建演示角色、一个 RM、一个已有 Client、一个案件和一个邀请。

## 当前范围

这个 repo 现在包含两个用户群体的真实 redirect flow：

- 前端使用 `@okta/okta-auth-js`，并处理 `/login/callback`
- 前端使用 `@azure/msal-browser`，并处理 `/rm/callback`
- API 根据配置的 Okta issuer 验证 Client bearer token
- API 根据配置的 Microsoft Entra ID tenant 验证 RM bearer token
- 受保护的演示端点：`GET /api/client/session`
- 受保护的 RM 端点：`GET /api/rm/session`
- 仅 RM 可调用的邀请端点：`POST /api/cases/invite`

## Okta 设置

使用一个 Okta SPA application，以及一个会为你的 API 签发 access token 的 authorization server。

前端配置放在 `web/.env`：

```bash
VITE_OKTA_ISSUER=https://your-domain.okta.com/oauth2/default
VITE_OKTA_CLIENT_ID=yourClientId
VITE_OKTA_REDIRECT_URI=http://localhost:5173/login/callback
VITE_OKTA_SCOPES=openid,profile,email,offline_access

VITE_ENTRA_TENANT_ID=your-tenant-id
VITE_ENTRA_CLIENT_ID=your-spa-application-client-id
VITE_ENTRA_REDIRECT_URI=http://localhost:5173/rm/callback
VITE_ENTRA_API_SCOPE=api://your-api-application-client-id/access_as_user
```

后端配置放在 `api/appsettings.Development.json`：

```json
{
  "Okta": {
    "Issuer": "https://your-domain.okta.com/oauth2/default",
    "Audience": "api://default"
  },
  "OktaManagement": {
    "BaseUrl": "https://your-domain.okta.com/",
    "ApiToken": "your-management-api-token"
  },
  "Entra": {
    "TenantId": "your-tenant-id",
    "Audience": "api://your-api-application-client-id"
  }
}
```

`OktaManagement.ApiToken` 需要有创建和激活用户的权限。官方文档中这些 lifecycle 操作对应 `okta.users.manage`。

在 Okta 中，确保 SPA app 允许：

- Sign-in redirect URI：`http://localhost:5173/login/callback`
- Sign-out redirect URI：`http://localhost:5173`
- 如果你的 org 设置需要，还要为前端 origin 配置 Trusted Origin

## Microsoft Entra ID 设置

前端使用一个 SPA app registration，后端使用一个 exposed API scope。

在 Microsoft Entra ID 中，确保 SPA app 允许：

- Redirect URI：`http://localhost:5173/rm/callback`
- Account type 符合内部 RM 用户的使用范围
- 对 exposed backend scope 的 API permission，例如 `api://your-api-application-client-id/access_as_user`

对于后端 API registration：

- 暴露一个 API scope，例如 `access_as_user`
- 使用 Application ID URI：`api://your-api-application-client-id`
- 将 `Entra.Audience` 设置为这个 Application ID URI
- RM 邀请表单会携带 Entra access token 调用 `POST /api/cases/invite`

## 真实邀请兑换

对于没有现有 Okta 账号的新客户：

1. RM 在 app 中提交邀请表单
2. API 使用 `activate=false` 创建 staged Okta user
3. API 使用 `sendEmail=false` 调用 Okta lifecycle activation
4. 返回的 `activationUrl` 会显示在 UI 中
5. Client 打开这个链接，完成真实的 Okta 激活流程

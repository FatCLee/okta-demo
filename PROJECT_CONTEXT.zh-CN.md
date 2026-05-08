# OKTA DEMO

English version: [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md)

这是一个演示项目，用来展示身份提供商（Identity Provider, IdP）如何集成到网站中。这个项目的产出会作为参考，被集成到一个正在开发中的现有项目里。该现有项目目前还没有 IdP 集成实现。

- Okta 用于外部客户。客户会收到邀请码来注册，也会通过一次性验证码登录，假设这些能力由 Okta 提供。
- Microsoft Entra ID 用于内部用户。

这个项目是一个银行 KYC 系统。内部用户（角色：RM）负责为外部用户（角色：Client）发起一个 KYC 案件。如果 RM 发现 Client 还没有已注册账号，RM 会用 Client 的邮箱作为用户名创建一个占位账号，并向 Client 发送邀请码。Client 可以登录已有账号，或者兑换邀请码注册新账号，然后继续完成自己的工作。

# Project Structure

## /web

这是项目的前端。

### Tech Choice

- React
- Vite
- 所有前端源码和配置都使用 TypeScript

### Content

- Login page
- RM page
- Client page

## /api

这是项目的后端 API。

### Tech Choice

- .NET 10+，以 Docker 容器方式托管
- Entity Framework，model first
- SQLite 用于本地演示持久化

### Content

- 基于角色的 API Controllers
- 用于维护用户角色的数据库实体
- 用于应用用户、角色分配、IdP 账号链接、KYC 案件和客户邀请的具体实体类

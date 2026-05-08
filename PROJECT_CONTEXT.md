# OKTA DEMO

中文版本：[PROJECT_CONTEXT.zh-CN.md](PROJECT_CONTEXT.zh-CN.md)

This is a demo project that showcase how Identity Providers will be integrated into a website. The outcome of this project will be used as a reference and integrated into a existing under-developement project, which does not have any implementation of IdP integration, knowing that 
- OKTA will be the option for external clients, where the clients will get invitation codes to sign up, also a one-time passcode to log-in, assuming it's provided by OKTA.
- Microsoft Entra ID, for internal users.

The project is a bank KYC system. The internal user (Role: RM) will be responsible to initiate a case for the external user (Role: Client). If RM finds out that the Client does not have an existing account registered, the RM will create a placeholder account for the Client with his/her email as user name, and send an invitation code to the Client. The Client will either login his/her existing account, or redeem the invitation code to register a new account, then continue his work. 

# Project Structure

## /web
This will be the frontend of the project.

### Tech Choice
- React
- Vite
- TypeScript for all frontend source code and config

### Content
- Login page
- RM page
- Client page

## /api
This will be the backend API of the project.

### Tech Choice
- .NET 10+ hosted as a docker container
- Entity Framework - model first
- SQLite for local demo persistence

### Content
- Role based API Controllers
- DB entities to maintain user roles
- Concrete entity classes for app users, role assignments, IdP account links, KYC cases, and client invites

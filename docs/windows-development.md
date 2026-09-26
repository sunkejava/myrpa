# Windows 本地开发与前端登录排查

本文对应 `src/frontend`（Vue / Vite）和 `src/backend/AgentRPA.Api`（.NET 10 / SQLite）。以下命令在 **PowerShell 7 或 Windows PowerShell** 中执行，均从仓库根目录开始。终端 A、B 分别保持运行。

## 1. 安装与检查

安装 .NET 10 SDK 和 Node.js 22 或更高版本。克隆仓库后检查：

```powershell
dotnet --version
node --version
npm --version
dotnet restore .\AgentRPA.sln
```

前端依赖在 `src/frontend` 下执行 `npm ci`，不要在仓库根目录运行 `npm run dev`。使用 `main` 的最新迁移，API 首次启动自动迁移数据库。

## 2. 启动 API（终端 A）

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5000'
dotnet run --project .\src\backend\AgentRPA.Api\AgentRPA.Api.csproj --no-launch-profile
```

等待 `Now listening on: http://127.0.0.1:5000`。浏览 `http://127.0.0.1:5000/swagger`。`appsettings.Development.json` 在 Development 环境预置 `admin / 123456`。账户仅在第一次连接到该数据库时创建；重复启动**不会重置已有 admin 密码**。生产环境不能使用该演示密码。

API 正常启动时会自动补齐国家/省/市/区、社保/医保/公积金及业务功能的默认目录。旧库升级后须重启 API；资源目录及工作流配置见 [默认资源与工作流配置](default-resources-and-workflows.md)。

默认连接 `Data Source=agentrpa.db`，相对路径由 API **进程工作目录**决定；从仓库根目录与 API 项目目录运行可能连到不同库。建议固定启动目录或指定绝对路径，例如在运行 API 前设置：

```powershell
$env:ConnectionStrings__AgentRPA = "Data Source=$((Resolve-Path .).Path)\agentrpa-dev.db"
```

数据库目录需要写入权限。

## 3. 启动前端（终端 B）

```powershell
cd .\src\frontend
npm ci
npm run dev -- --host 127.0.0.1
```

访问 `http://127.0.0.1:5173/`，输入 `admin` / `123456`。Vite 的 `/api` 代理默认指向 `http://127.0.0.1:5000`。如果 API 端口不同，在**启动 Vite 之前**设置 `$env:VITE_API_PROXY_TARGET = 'http://127.0.0.1:你的端口'`，然后重启 Vite。不要直接打开 `src/frontend/index.html` 或 `dist/index.html`。

例如 API 监听 **HTTP 8666**、前端监听 5173，可在终端 A 设置 `$env:ASPNETCORE_URLS = 'http://127.0.0.1:8666'`，在终端 B 启动 Vite 前设置：

```powershell
$env:VITE_API_PROXY_TARGET = 'http://127.0.0.1:8666'
npm run dev -- --host 127.0.0.1
```

截图中的 `http://127.0.0.1:5173/api/auth/login` 被重定向到 `https://127.0.0.1:8666/api/auth/login`，随后出现 CORS 报错，说明 API 对开发代理的 HTTP 请求执行了 HTTPS 重定向。更新后的 API 在 Development 环境不重定向 HTTP；**重启 API** 后验证 `curl.exe -i http://127.0.0.1:8666/api/auth/me`：预期是 401，响应不能含 `Location: https://...`。若仍为 307/308，请检查实际启动的 API 是否是最新代码、环境是否为 Development，以及其他反向代理是否配置重定向。若你的 8666 本身仅提供 HTTPS，则改用 `https://127.0.0.1:8666` 作为代理目标，并确保 Node.js 信任开发证书；不要把仅支持 HTTPS 的端口写成 `http://`。

在仓库根目录使用 PowerShell 验证后端及代理：

```powershell
Invoke-WebRequest http://127.0.0.1:5000/swagger/v1/swagger.json | Select-Object StatusCode
$body = @{ userName = 'admin'; password = '123456' } | ConvertTo-Json
Invoke-RestMethod http://127.0.0.1:5000/api/auth/login -Method Post -ContentType 'application/json' -Body $body
Invoke-RestMethod http://127.0.0.1:5173/api/auth/login -Method Post -ContentType 'application/json' -Body $body
$session = Invoke-RestMethod http://127.0.0.1:5173/api/auth/login -Method Post -ContentType 'application/json' -Body $body
Invoke-RestMethod http://127.0.0.1:5173/api/auth/me -Headers @{ Authorization = "Bearer $($session.accessToken)" }
```

两次登录应返回 `accessToken`，`/api/auth/me` 应返回用户名和角色。

## 4. 登录故障定位

| 现象 | 核对项与处理 |
| --- | --- |
| API 地址无法访问 | 检查终端 A 的监听地址、API 错误和 `Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue`。先让 Swagger 正常。 |
| 直接访问 API 可登录，通过 Vite 登录显示网络错误或 502 | 浏览器开发者工具 Network 查看 `/api/auth/login` URL，应为 `127.0.0.1:5173`；核对 Vite 控制台代理错误及两个端口。修改 `VITE_API_PROXY_TARGET` 后必须重启 Vite。 |
| 登录请求跳到 HTTPS 8666 并报 CORS | 检查 `curl.exe -i http://127.0.0.1:8666/api/auth/me` 是否返回 307/308 及 `Location`。切到最新 API、确保 Development 环境，重启 API；若 8666 只监听 HTTPS，代理目标也必须是 HTTPS 并信任证书。 |
| 登录返回 401 | 确认 API 环境是 Development，核对实际 SQLite 文件位置。已有 admin 不会被初始脚本覆盖；可换新的**开发专用数据库路径**验证预置密码，不要删除有业务数据的旧库。CI 测试库可能使用不同密码。 |
| 登录返回 500 | 查看终端 A 的异常堆栈、数据库可写性和迁移；运行 `dotnet build .\AgentRPA.sln -c Debug`。 |
| 登录后刷新回到登录页 | `sessionStorage` 令牌仅在当前标签页保存，重载会查询 `/api/auth/me`。检查它是否 401。开发环境若未配置固定 JWT 签名密钥，API 每次重启会生成新密钥，旧令牌随之失效，应重新登录。 |
| NodeAgent 连接异常 | 不影响登录。任务执行才需要设置节点注册密钥、节点 API 地址和管理员审批。 |

## 5. 构建及浏览器验收

```powershell
dotnet build .\AgentRPA.sln -c Release
cd .\src\frontend
npm run build
npx playwright install chromium
npm run test:e2e
```

仓库的端到端用例使用独立的开发测试库，CI 将 `AgentRPA__Bootstrap__AdminPassword` 设为 `BrowserTestPassword123!`，并要求 API 在 `127.0.0.1:5000`。本机运行原有集成测试时要使用与 CI 一致的测试密码与 NodeAgent 配置，不能把该密码误当成普通开发库的默认密码。完整条件参考 `.github/workflows/frontend-build.yml`。

## 6. 配置优先级

`appsettings.json` 是基础配置；Development 环境叠加 `appsettings.Development.json`；环境变量可覆盖对应值。双下划线 `__` 对应 JSON 的冒号，例如 `$env:AgentRPA__Bootstrap__AdminPassword='自定义开发密码'` 只影响首次创建 admin。`VITE_API_PROXY_TARGET` 属于 Vite 进程，不由 .NET 读取。

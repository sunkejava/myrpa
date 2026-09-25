# AgentRPA

> 面向政务、社保及企业业务系统的智能 Agent RPA 平台。自然语言负责理解业务，权限负责边界，Workflow 保证确定性，Scheduler 负责资源选择，NodeAgent 负责实际执行。

## 架构

```text
自然语言 → Agent/TaskPlan → Permission → Confirmation
                         ↓
              Task → TaskItem → Execution
                         ↓
                    Scheduler
                         ↓
          NodePool → ExecutionNode → WorkerSlot
                         ↓
                     NodeAgent
                         ↓
                  RPA Engine
```

### 一托 N 异构节点

```text
                 AgentRPA Server
                       │
          ┌────────────┼────────────┐
          │            │            │
        Agent       Scheduler   Node Registry
                                    │
              ┌─────────────────────┼─────────────────┐
              ▼                     ▼                 ▼
        Windows VM             Windows PC          Linux
        NodeAgent              NodeAgent            NodeAgent
           │                     │                    │
       Worker × N             Worker × N           Worker × N
```

- `ExecutionNode`：Physical / VirtualMachine / CloudDesktop / Container。
- `OsPlatform`：Windows / Linux / macOS / Other，与 NodeKind 独立。
- `WorkerSlot`：节点上的并发执行槽位。
- `NodePool`：按城市、网络、环境、业务组织执行节点。
- `NodeCapability`：Browser、DesktopUI、Office、UKey、SmartCard 等能力。
- `ExecutionRequirement`：业务系统 / Workflow / Task 的执行环境约束。
- `AgentKey`：NodeAgent 稳定身份。
- `Lease / ResourceLock`：用于 WorkerSlot、UKey 等独占资源。

## 调度

Scheduler **先硬过滤，再软评分**。

硬约束包括：Online、WorkerSlot、OS、NodeKind、Browser、DesktopUI、NodePool、NetworkZone、Capability、Hardware/UKey、固定/排除节点以及资源权限。

无可用资源进入 `WaitingForResource`，不会随机派发到其他机器。

## 权限

```text
User → Role → AccessPolicy → City → BusinessSystem → BusinessFunction → Action → NodePool / ExecutionNode
```

例如可以精确限制：某人员只能操作“北京 → 北京社保 → 社保缴费查询 → Execute”，并进一步限制允许使用的执行节点资源池。

## 外部集成

Provider / Adapter 用于隔离外部服务：

- UKey / USB Key：发现、状态、证书/签名等受控操作。
- Captcha：多供应商识别、超时、重试、限流、故障切换。
- QR 登录：生成短时安全入口，通知用户扫码后恢复任务。
- Face / 实名认证：HumanIntervention，不绕过第三方认证。
- Notification：Email、钉钉、微信、企业微信、WebSocket/站内通知，预留 SMS/Webhook。

## 技术栈

- .NET 10 / C#
- DDD / Clean Architecture
- ASP.NET Core Web API
- EF Core 10 + SQLite（当前默认开发数据库）
- ASP.NET Core SignalR：Server ↔ NodeAgent
- Swagger / OpenAPI
- Vue 3 + TypeScript（前端建设中）

## 开发启动

部署产物由 GitHub Actions 的 Release Packages 流水线生成，Linux/Windows 的启动和节点浏览器依赖见 [发布包部署说明](docs/deployment.md)。

需要 .NET 10 SDK 与 Node.js 22。首次运行 API 自动执行 EF Core Migration；默认数据库是 API 工作目录下的 SQLite 文件。

```bash
export AgentRPA__Bootstrap__AdminPassword='自行设置至少十位的密码'
export AgentRPA__Jwt__SigningKey='自行设置至少三十二位的随机签名密钥'
dotnet run --project src/backend/AgentRPA.Api --urls http://127.0.0.1:5000
```

另开终端启动前端：

```bash
cd src/frontend
npm install
npm run dev
```

访问 `http://localhost:5173`，开发环境 `/api` 默认代理到 `http://localhost:5000`，可通过 `VITE_API_PROXY_TARGET` 覆盖。Swagger 在开发环境的 `http://localhost:5000/swagger`。首次启动的管理员账号为 `admin`，密码为配置的 `AgentRPA__Bootstrap__AdminPassword`。生产环境必须显式设置 JWT 签名密钥；不要将密码提交到仓库。

当前前端接入登录、本人任务列表、自然语言规划和任务提交；其余导航与业务管理能力仍按 `docs/development-plan.md` 逐阶段开发。CI 除编译外验证 API 启动、登录和匿名访问控制。

```text
src/backend/
├── AgentRPA.Domain/
├── AgentRPA.Application/
├── AgentRPA.Infrastructure/
├── AgentRPA.Api/
├── AgentRPA.Worker/
├── AgentRPA.Contracts/
└── AgentRPA.NodeAgent/
```

## 当前开发进度

项目已具备账户登录、任务调度、Workflow、节点注册、浏览器执行和基础权限校验。前端已接入登录、本人任务列表与自然语言规划。城市资源树、逐 Step 权限、审批、真实社保 Adapter、恢复机制及完整运营页面仍在开发中；准确的剩余事项参见 `docs/development-plan.md`。

本仓库的 CI 分别运行后端 Build/Test、前端 Build，以及 API 启动与登录冒烟检查。

## 安全原则

- 密码、Token、Cookie、UKey PIN、私钥禁止进入普通日志。
- 凭据与 Workflow 分离存储。
- NodeAgent 不执行未经服务端授权的任意脚本。
- QR/人工认证入口绑定 Task/Intervention，并具备 TTL 和一次性控制。
- UKey 等独占硬件必须通过 ResourceLock 管理。
- Task、节点派发、人工介入、外部调用进入审计链路。

## 文档

- `docs/requirements.md`：产品需求。
- `docs/architecture.md`：总体架构。
- `docs/node-scheduling.md`：一托 N、NodePool、Capability、Scheduler、Lease。
- `docs/permission-model.md`：城市/系统/功能/Action/资源权限。
- `docs/agent-task-flow.md`：Agent → TaskPlan → Task → Execution。
- `docs/rpa-workflow.md`：Workflow / Step / Version。
- `docs/integration-capabilities.md`：UKey、Captcha、QR、Face、Notification Provider。
- `docs/security.md`：安全、凭据、审计、人工介入。
- `docs/data-model.md`：核心数据模型。
- `docs/api-design.md`：API 设计。
- `docs/ui-requirements.md`：前端规范。
- `docs/development-plan.md`：实时开发进度。

## 核心理念

> **让 AI 理解业务，让权限控制边界，让 Scheduler 选择资源，让 Workflow 保证确定性，让 NodeAgent 执行，让审计记录全过程。**

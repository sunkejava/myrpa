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

**Phase 1：平台基础 + 一托 N 执行节点。** 当前已完成持久化 Registry 和 NodeAgent SignalR 通信骨架，下一步进入节点认证、Lease/ResourceLock、真实 ExecutionDispatch。

### 已完成

- [x] 一托 N 异构执行节点总体架构
- [x] `ExecutionNode / NodePool / NodeCapability / WorkerSlot`
- [x] `NodeKind / OsPlatform / AgentKey`
- [x] `ExecutionRequirement` + Scheduler 硬过滤/软评分
- [x] Node 注册/心跳 Contracts 与 API
- [x] EF Core 10 + SQLite DbContext
- [x] City / BusinessSystem / BusinessFunction 持久化映射
- [x] Node Registry EF Core 持久化实现
- [x] WorkerSlot / NodeCapability 注册与刷新
- [x] 最近心跳节点筛选
- [x] SignalR Hub + typed NodeAgent contract
- [x] NodeAgent 注册 → SignalR Connect
- [x] NodeAgent 自动重连与命令接收骨架
- [x] GitHub Actions restore/build 基础 CI

### 正在开发

- [ ] API Key / mTLS / 证书认证
- [ ] 节点注册审批 / Disabled 控制
- [ ] NodePool / Node / WorkerSlot 管理 API
- [ ] 节点离线自动落库
- [ ] NodeLease / ResourceLock
- [ ] SignalR Heartbeat / ACK / Progress 完整协议
- [ ] ExecutionDispatch
- [ ] Scheduler 接入真实 Lease

### 后续阶段

- [ ] Workflow 编辑、版本和发布
- [ ] Playwright Browser Worker
- [ ] Windows Desktop UI Worker
- [ ] Task / TaskItem / Execution 状态机
- [ ] Excel/CSV 批量执行、断点续跑、跨 Node 并发
- [ ] UKey / Captcha / Human Intervention / Notification Provider 实现
- [ ] Agent 自然语言规划与权限预检查
- [ ] 前端节点、调度、执行、人工介入运营中心
- [ ] 集成测试及 Windows NodeAgent 发布

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

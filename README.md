# AgentRPA

> 面向政务、社保及企业业务系统的智能 Agent RPA 平台。通过自然语言理解任务，由服务端进行权限校验、工作流编排与资源调度，再将具体执行任务安全派发到 Windows / Linux / 虚拟机 / 云桌面等异构执行节点。

## 项目定位

AgentRPA 不是简单的“录制网页操作”工具，而是 **Agent + 权限 + Workflow + Scheduler + NodeAgent + RPA Engine** 的企业级自动化平台。

典型场景：用户描述“登录 A 单位社保网站，读取目录下 Excel 中人员信息，完成对应业务”。系统识别城市、业务系统、业务功能和参数，完成权限校验后匹配确定性 Workflow，并根据执行要求自动选择合适的执行节点。

## 1. 总体架构

```text
自然语言请求 → Agent / Intent → TaskPlan → Permission → Confirmation
                                      ↓
                              Task / TaskItem / Execution
                                      ↓
                                  Scheduler
                                      ↓
                         NodePool / ExecutionNode / WorkerSlot
                                      ↓
                                  NodeAgent
                                      ↓
                                  RPA Engine
                                      ↓
                       Browser / Desktop / UKey / File / Captcha
                                      ↓
                              结果 / 审计 / 通知
```

### 1.1 一托 N 异构执行节点

```text
                         AgentRPA Server
                              │
             ┌────────────────┼─────────────────┐
             │                │                 │
           Agent          Scheduler        Node Registry
             │                │                 │
             └────────────────┴──────► Task Queue
                                           │
                    ┌──────────────────────┼─────────────────────┐
                    ▼                      ▼                     ▼
              Windows VM            Windows Physical          Linux
              NodeAgent              NodeAgent                NodeAgent
                 │                      │                       │
             Worker × N              Worker × N              Worker × N
```

- `ExecutionNode`：物理机、虚拟机、云桌面或容器等实际执行环境。
- `WorkerSlot`：节点上的并发执行槽位。
- `NodePool`：按城市、网络、环境、业务等组织节点。
- `NodeCapability`：描述浏览器、Desktop UI、Office、UKey、智能卡等能力。
- `ExecutionRequirement`：描述业务系统/Workflow/Task 对执行环境的约束。
- `Lease / ResourceLock`：保证 WorkerSlot、UKey 等独占资源不会被多个任务同时占用。
- `AgentKey`：NodeAgent 的稳定节点身份，不以机器名称作为唯一身份。

`NodeKind` 与 `OsPlatform` 分开建模，例如 `VirtualMachine + Windows` 表示 Windows 虚拟机，`Physical + Windows` 表示 Windows 物理机。

### 1.2 调度规则

Scheduler 先硬过滤，再软评分。

**硬约束：**在线状态、WorkerSlot、OS、NodeKind、浏览器、Desktop UI、NodePool、NetworkZone、Required Capability、Required Hardware/UKey、固定/排除节点及执行资源权限。

**软评分：**当前负载、Slot 利用率、节点/NodePool 优先级、硬件亲和性、网络延迟、Workflow/Browser 缓存亲和性、历史失败率等。

无候选资源时任务进入 `WaitingForResource`，不会随机选择机器。

## 2. 权限模型

```text
User → Role → AccessPolicy → City → BusinessSystem → BusinessFunction → Action → NodePool / ExecutionNode
```

权限必须在 Agent 规划后、RPA/NodeAgent 派发前校验，技术上可用的节点也不能绕过业务授权。

## 3. 集成能力

通过 Provider / Adapter 隔离外部服务和硬件差异：

- **UKey / USB Key**：发现、状态检测、证书/签名等受控操作；UKey 同时属于 Capability 和独占资源。
- **验证码识别**：`ICaptchaProvider`，支持多供应商、超时、重试、限流、健康检查和故障切换。
- **二维码登录**：生成短时、一次性安全入口，通过通知渠道交给用户扫码，完成后恢复任务。
- **人脸/实名认证**：作为 HumanTask/HumanIntervention，不绕过第三方认证，不保存不必要的生物特征模板。
- **通知中心**：Email、钉钉、微信、企业微信、WebSocket/站内通知，预留 SMS/Webhook。

## 4. 执行模型

```text
BusinessSystem → ExecutionRequirement → WorkflowVersion
       → Task → TaskItem → Execution → ExecutionDispatch
       → ExecutionNode → WorkerSlot → RPA Engine
```

批量任务使用 `Task + TaskItem`，每个数据项独立状态和重试。100 条数据中只有 4 条失败时，只重试失败项。

## 5. 核心模块

| 模块 | 职责 |
|---|---|
| Agent / Planner | 自然语言理解、意图识别、TaskPlan |
| Permission Engine | 城市/系统/功能/Action/资源权限 |
| Workflow | 确定性流程及版本 |
| Scheduler | 一托 N 节点和 WorkerSlot 调度 |
| Node Registry | 节点注册、心跳、能力、状态 |
| NodeAgent | 节点侧通信、Worker 管理和本地执行 |
| RPA Engine | Browser/Desktop 确定性执行 |
| Credential | 账号、证书、UKey 等敏感凭据 |
| Human Intervention | 二维码、短信、人脸、人工认证接管 |
| Captcha | 第三方验证码识别 |
| Notification | Email/钉钉/微信/企业微信/站内通知 |
| Task Center | Task/TaskItem/Execution、队列、重试、暂停、恢复 |
| Audit / Monitor | 审计、日志、状态、成功率、耗时、异常 |

## 6. 后端技术栈与目录

- .NET 10 / C#
- DDD / Clean Architecture
- ASP.NET Core Web API
- EF Core 10 + SQLite（当前默认开发数据库，生产可扩展其他 Provider）
- SignalR：Server ↔ NodeAgent（建设中）
- JWT / Node 身份认证（建设中）
- Swagger / OpenAPI

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

## 7. 安全原则

- 密码、Token、Cookie、UKey PIN、私钥等禁止进入普通日志。
- 凭据与 Workflow 分离存储。
- NodeAgent 不接受未经服务端授权的任意脚本。
- QR/人工认证入口绑定 Task/Intervention，具有 TTL、一次性使用和安全访问控制。
- UKey 使用 ResourceLock 防止并发占用。
- Task、节点派发、人工介入、外部调用进入审计链路。

## 8. 文档

- `docs/requirements.md`：产品需求规格。
- `docs/architecture.md`：总体技术架构。
- `docs/node-scheduling.md`：一托 N、NodePool、Capability、Scheduler、Lease。
- `docs/permission-model.md`：城市/系统/功能/操作/资源权限。
- `docs/agent-task-flow.md`：Agent → TaskPlan → Task → Execution。
- `docs/rpa-workflow.md`：Workflow / Step / Version。
- `docs/integration-capabilities.md`：UKey、验证码、二维码、人脸、通知 Provider。
- `docs/security.md`：安全、凭据、审计、人工介入。
- `docs/data-model.md`：核心数据模型。
- `docs/api-design.md`：API 设计。
- `docs/ui-requirements.md`：前端 UI 规范。
- `docs/development-plan.md`：分阶段开发计划和实时完成进度。

## 9. 当前开发进度

**当前阶段：Phase 1 — 平台基础与异构执行节点。当前已完成 Node Registry 的 EF Core 持久化基础，下一步进入节点认证、租约和 SignalR。**

### 已完成

- [x] 总体架构与一托 N 异构执行节点设计
- [x] 城市 → 系统 → 功能 → Action 权限模型设计
- [x] Workflow / WorkflowVersion / Step 设计
- [x] UKey / 验证码 / 二维码 / 人脸 / 通知 Provider 设计
- [x] `ExecutionNode / NodePool / NodeCapability / WorkerSlot` 领域模型
- [x] `ExecutionRequirement` 与约束合并规则
- [x] Scheduler 硬过滤 + 软评分骨架
- [x] `ExecutionCommand / ExecutionProgress` 契约
- [x] NodeAgent 独立项目骨架
- [x] `AgentRPA.sln` / `Directory.Build.props`
- [x] EF Core 10 + SQLite 基础设施
- [x] City / BusinessSystem / BusinessFunction 持久化映射
- [x] Node Registry 持久化实现
- [x] AgentKey 稳定节点身份
- [x] 节点注册/心跳 API 接入 Registry
- [x] WorkerSlot 与 NodeCapability 注册/刷新
- [x] GitHub Actions restore/build 基础 CI

### 正在开发

- [ ] 节点身份认证与注册审批
- [ ] NodePool / Node / WorkerSlot 查询管理 API
- [ ] 节点离线自动落库
- [ ] NodeLease / ResourceLock 持久化
- [ ] SignalR Server ↔ NodeAgent 通信
- [ ] ExecutionDispatch 实际派发
- [ ] Scheduler 接入真实 Lease

### 后续

- [ ] Task / TaskItem / Execution 状态机
- [ ] 权限引擎实际实现
- [ ] Workflow 执行引擎
- [ ] Playwright 浏览器执行器
- [ ] Windows Desktop UI 执行器
- [ ] UKey / Captcha Provider
- [ ] Human Intervention / QR / Face 流程
- [ ] Notification Provider
- [ ] 前端节点/调度/执行监控
- [ ] 集成测试及 Windows NodeAgent 发布

## 10. 核心理念

> **让 AI 理解业务，让权限控制边界，让 Scheduler 选择资源，让 Workflow 保证确定性，让 NodeAgent 执行，让审计记录全过程。**

当前代码已经进入可持久化基础阶段，但仍不是生产完成版；详细进度以 `docs/development-plan.md` 为准。

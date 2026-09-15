# AgentRPA 总体技术架构

## 1. 架构原则

AgentRPA 采用分层、领域驱动、Provider/Adapter 插件化和分布式执行节点设计，核心原则是：

> Agent 负责理解，Permission Engine 负责授权，Workflow 负责确定性流程，Scheduler 负责选择执行节点，RPA Worker 负责实际执行。

禁止让 LLM 直接成为最终执行控制器。

平台采用 **一台服务器管理 N 个执行节点** 的模式。执行节点可以是 Windows 物理机、Windows 虚拟机、Linux 主机或其他受支持运行环境。服务器不假设所有网站都能在同一种环境运行，而是根据业务系统/Workflow 的执行环境要求进行节点匹配。

## 2. 推荐架构

```text
┌──────────────────────────────────────────────────────────────┐
│                         Web / Vue                            │
│ 对话 · 任务 · 系统 · Workflow · 节点 · 监控 · 人工介入       │
└────────────────────────────┬─────────────────────────────────┘
                             │
┌────────────────────────────▼─────────────────────────────────┐
│                       ASP.NET Core API                        │
└───────┬───────────┬───────────┬───────────────┬──────────────┘
        │           │           │               │
   Agent Core   Permission   Task API      Node API
        │         Engine        │               │
        ▼           │           ▼               ▼
   Task Planner ────┘      Task Scheduler   Node Registry
                                │               │
                                ▼               │
                         Execution Scheduler    │
                                │               │
                  ┌─────────────┴─────────────┐ │
                  ▼                           ▼ │
          Worker Pool / Queue            Capability
                  │                     Matching Engine
        ┌─────────┼──────────┐                │
        ▼         ▼          ▼                │
   Node A      Node B      Node N ◄───────────┘
   Windows VM  Windows PC  Linux/Other
        │         │          │
        ▼         ▼          ▼
   RPA Runtime  RPA Runtime RPA Runtime
        │         │          │
        ├──Browser Engine────┤
        ├──File Engine───────┤
        ├──UKey Provider─────┤
        ├──Captcha Provider──┤
        └──Human Intervention┘
                  │
                  ▼
             外部业务系统
```

## 3. 一托 N 执行节点

服务器端是控制平面（Control Plane），执行节点是数据/执行平面（Execution Plane）。

```text
                    AgentRPA Server
                         │
             ┌───────────┼───────────┐
             ▼           ▼           ▼
          Node-001     Node-002    Node-00N
          Windows VM   Windows PC  Linux
             │           │           │
          Worker×N     Worker×N    Worker×N
```

### 3.1 Node 与 Worker 的职责

- **Node**：代表一台可被调度的执行环境，负责注册、心跳、能力上报、资源状态和安全身份。
- **Worker**：Node 上实际运行 Workflow 的执行进程/执行槽位。
- 一个 Node 可以有多个 Worker 执行槽位，但受 CPU、内存、浏览器数量、UKey、并发会话等资源约束。
- 一个 Server 可以管理任意数量的 Node，最终通过节点池实现横向扩展。

### 3.2 不把“虚拟机”和“Windows”混为一个概念

执行调度需要同时描述：

- `NodeKind`：Physical、VirtualMachine、Container、CloudDesktop 等。
- `OsPlatform`：Windows、Linux、macOS 等。
- `Architecture`：x64、arm64 等。
- `RuntimeCapabilities`：Browser、DesktopUI、Office、UKey、Certificate、Camera、FileSystem 等。
- `NetworkZone`：内网、专网、互联网、指定出口等。

因此“Windows 虚拟机”应该表达为：

```text
NodeKind = VirtualMachine
OsPlatform = Windows
```

而不是简单增加一个“虚拟机类型”。这样以后支持 Windows 物理机、Windows 云桌面、Linux 容器时无需重新设计模型。

## 4. 后端分层

```text
src/
├── AgentRPA.Domain
├── AgentRPA.Application
├── AgentRPA.Infrastructure
├── AgentRPA.Api
├── AgentRPA.Worker
└── AgentRPA.Contracts
```

### Domain

保存 City、BusinessSystem、BusinessFunction、Workflow、Permission、Task、ExecutionNode 等领域模型和领域规则。

### Application

实现用例编排，例如 CreateTask、PlanTask、CheckPermission、ResolveExecutionTarget、ScheduleTask、ExecuteTask、RetryTask。

### Infrastructure

数据库、浏览器、LLM、文件系统、消息队列、节点通信、凭据加密等技术实现。

### API

对 Web 前端和第三方调用提供 HTTP API，并提供 Node 注册、心跳、任务领取和人工介入等控制平面接口。

### Worker

运行后台任务、Workflow 和 RPA 执行。未来可以将 Worker 作为独立 Windows Agent/VM Agent 部署。

### Contracts

保存 API DTO、TaskPlan、Workflow Schema、Node Capability、Execution Contract 等跨层契约。

## 5. Agent 架构

```text
User Message
 ↓
Intent Analyzer
 ↓
Entity Resolver
 ↓
Parameter Extractor
 ↓
Permission Precheck
 ↓
Task Planner
 ↓
Workflow Resolver
 ↓
Execution Requirement Resolver
 ↓
Task Confirmation
 ↓
Task Queue
 ↓
Execution Scheduler
```

Agent 可以理解“在北京社保系统执行查询”，但不负责决定具体使用哪台电脑。执行节点选择必须由 Scheduler 根据系统/Workflow 的要求、节点能力、资源状态、租约和权限统一完成。

## 6. 执行环境约束

业务系统和 Workflow 必须支持声明执行环境要求。

例如：

```json
{
  "os": ["Windows"],
  "nodeKinds": ["VirtualMachine", "Physical"],
  "browser": ["Edge", "Chrome"],
  "desktopUi": true,
  "uKey": true,
  "networkZone": "GovernmentNetwork"
}
```

不同系统可以配置不同要求：

| 业务系统 | 执行环境 | 示例 |
|---|---|---|
| A 社保 | Windows VM | 必须 Windows，允许 VM |
| B 政务平台 | Windows Physical | 必须 Windows 物理机，禁止 VM |
| C 查询平台 | 任意支持浏览器的节点 | Windows/Linux 均可 |
| D UKey 系统 | Windows + UKey | 必须具备指定 UKey 能力 |
| E 内网系统 | 指定网络区域 | 必须 GovernmentNetwork |

### 6.1 约束继承

推荐优先级：

```text
BusinessSystem 默认要求
        ↓ 覆盖/补充
WorkflowVersion 要求
        ↓ 覆盖/补充
Task 临时要求（仅允许更严格）
```

Task 不允许通过自然语言随意降低安全约束。例如系统要求 Windows，则用户不能通过“随便找一台 Linux 执行”绕过限制。

## 7. Execution Scheduler

Scheduler 是一托 N 架构的核心组件。

```text
Task Queued
    ↓
Resolve Workflow
    ↓
Resolve ExecutionRequirement
    ↓
Find Eligible Nodes
    ↓
Filter Permission / Tenant / Network / Capability
    ↓
Filter Health / Capacity / Lease
    ↓
Score Nodes
    ↓
Acquire Node + Worker Lease
    ↓
Dispatch Task
    ↓
Worker Execute
    ↓
Heartbeat / Progress
    ↓
Completed / Failed / WaitingHuman
```

### 7.1 节点匹配必须是“能力匹配”，而不是简单轮询

错误方式：

```text
Task → Node1 → Node2 → Node3
```

正确方式：

```text
Task Requirements
      ↓
OS / NodeKind / Browser / UKey / Network / DesktopUI
      ↓
Eligible Nodes
      ↓
Health + Capacity + Affinity + Priority
      ↓
Best Node
```

### 7.2 推荐评分因素

- OS/Runtime 能力完全匹配
- NodeKind 匹配
- 网络区域匹配
- 浏览器版本匹配
- UKey/证书能力匹配
- Worker 空闲度
- Node 健康状态
- 当前并发数
- 业务系统节点亲和性
- 用户/组织节点权限
- 任务优先级

## 8. 节点生命周期

```text
Created
 ↓
PendingRegistration
 ↓
Online
 ↓
Busy
 ↓
Idle
 ↓
Offline / Unhealthy
 ↓
Draining
 ↓
Disabled
```

节点必须通过注册获得 NodeId 和安全身份。运行期间通过心跳上报：

- NodeId
- AgentVersion
- OS
- Architecture
- Capabilities
- BrowserVersion
- UKey 状态
- CPU/Memory
- Worker 数量
- RunningTask 数量
- NetworkZone
- LastHeartbeatAt

节点离线后不得继续派发新任务；已经运行的任务根据 Lease/Heartbeat 策略决定恢复、迁移或进入人工处理。

## 9. Task / Execution / Node 的关系

```text
Task
 ├── TaskItem 1
 │     └── Execution → Node A / Worker 1
 ├── TaskItem 2
 │     └── Execution → Node B / Worker 2
 └── TaskItem N
       └── Execution → Node A / Worker 3
```

因此批量任务天然支持跨节点并行。

例如 Excel 有 100 人：

```text
Server
 ├── Windows VM 01 → 30 items
 ├── Windows VM 02 → 30 items
 ├── Windows PC 01 → 20 items
 └── Linux Node 01  → 20 items
```

但如果业务系统要求 Windows，则 Linux 节点自动被排除。

## 10. Node Affinity 与独占资源

某些业务不能简单地在任意满足 OS 的节点执行，因此需要支持：

- `PreferredNodeIds`
- `RequiredNodeIds`
- `ExcludedNodeIds`
- `NodePoolId`
- `ResourceLock`
- `CredentialAffinity`
- `UKeyAffinity`
- `NetworkZone`

例如一个 UKey 已插在 Windows PC-01：

```text
BusinessSystem = 某电子政务系统
RequiredCapabilities = Windows + UKey
UKey = Device-001
        ↓
Scheduler
        ↓
Node-01（拥有 Device-001）
```

此时不能把任务随机派到其他 Windows 节点。

## 11. Workflow Engine

Workflow 由结构化 Step 组成，例如：

```json
{
  "version": 3,
  "executionRequirements": {
    "os": ["Windows"],
    "desktopUi": true
  },
  "steps": [
    { "type": "OpenPage", "url": "..." },
    { "type": "Input", "target": "username", "value": "{{credential.username}}" },
    { "type": "Input", "target": "password", "value": "{{credential.password}}" },
    { "type": "Click", "target": "login" },
    { "type": "WaitForElement", "target": "dashboard" }
  ]
}
```

生产环境应对 Workflow Schema 做严格校验，禁止执行未注册或未审核的 Step 类型。

## 12. Browser Engine

浏览器引擎作为独立抽象层，不让业务代码直接依赖具体浏览器 API。

建议接口：

```text
IBrowserSession
IBrowserPage
IElementLocator
IBrowserAction
IFileTransfer
```

这样可以替换 Playwright、Chromium 或其他实现。

## 13. Task Engine

任务引擎负责：

- 状态机
- 队列
- 并发控制
- 超时
- CancellationToken
- RetryPolicy
- TaskItem
- Worker Lease
- Node Lease
- 断点恢复
- 跨节点重新调度

长任务必须采用持久化状态，而不能只依赖内存状态。

## 14. Worker

推荐 Worker 使用租约机制：

```text
Queued
 ↓
Scheduler Select Node
 ↓
Worker Acquire Lease
 ↓
Running
 ↓
Heartbeat
 ↓
Completed / Failed
```

Worker 意外退出后，超过 LeaseTimeout 的任务可以重新进入 Scheduler，但必须根据 Workflow 是否产生不可逆业务操作决定是否自动重试。

## 15. 外部系统适配器

不同城市、不同业务系统建议通过 Adapter 组织：

```text
Adapters/
├── BeijingSocialSecurity
├── BeijingMedical
├── ShanghaiSocialSecurity
└── ...
```

但业务权限仍统一绑定：

```text
City + System + Function
```

Adapter 只负责技术适配，不负责权限判断。

## 16. LLM Provider

通过统一接口支持不同模型：

```text
ILLMProvider
├── OpenAICompatible
├── LocalModel
└── OtherProvider
```

Agent 输出必须经过 JSON Schema/Contract 验证，再进入 TaskPlan。

## 17. 事件

推荐领域/集成事件：

```text
TaskCreated
TaskPlanned
PermissionDenied
TaskQueued
TaskDispatching
TaskDispatched
NodeLeaseAcquired
TaskStarted
TaskPaused
HumanInterventionRequested
TaskResumed
TaskItemCompleted
TaskItemFailed
TaskCompleted
TaskFailed
NodeOnline
NodeOffline
NodeCapabilityChanged
WorkflowVersionPublished
```

后续可以使用消息队列实现跨进程任务通知和事件驱动。

## 18. 可观测性

统一记录：

- TraceId
- TaskId
- ExecutionId
- WorkflowId
- WorkflowVersion
- UserId
- WorkerId
- NodeId
- NodeKind
- OsPlatform
- StepId
- Duration
- Result

敏感数据不得直接进入日志。

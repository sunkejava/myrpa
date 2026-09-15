# AgentRPA 总体技术架构

## 1. 架构原则

AgentRPA 采用分层、领域驱动和插件化设计，核心原则是：

> Agent 负责理解，Permission Engine 负责授权，Workflow 负责确定性流程，RPA Engine 负责执行。

禁止让 LLM 直接成为最终执行控制器。

## 2. 推荐架构

```text
┌──────────────────────────────────────────┐
│                Web / Vue                  │
│ 对话 · 任务 · 系统 · Workflow · 监控      │
└───────────────────┬──────────────────────┘
                    │
┌───────────────────▼──────────────────────┐
│              ASP.NET Core API             │
└───────┬───────────┬───────────┬──────────┘
        │           │           │
   Agent Core   Permission   Task API
        │         Engine        │
        ▼           │           ▼
   Task Planner ────┘      Task Scheduler
        │                       │
        ▼                       ▼
   Workflow Resolver       Queue / Worker
                                │
                                ▼
                         RPA Execution Engine
                                │
                ┌───────────────┼───────────────┐
                ▼               ▼               ▼
          Browser Engine    File Engine    Human UI
                │
                ▼
          外部业务系统
```

## 3. 后端分层

推荐：

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

保存 City、BusinessSystem、BusinessFunction、Workflow、Permission、Task 等领域模型和领域规则。

### Application

实现用例编排，例如 CreateTask、PlanTask、CheckPermission、ExecuteTask、RetryTask。

### Infrastructure

数据库、浏览器、LLM、文件系统、消息队列、凭据加密等技术实现。

### API

对 Web 前端和第三方调用提供 HTTP API。

### Worker

运行后台任务、Workflow 和 RPA 执行。

### Contracts

保存 API DTO、TaskPlan、Workflow Schema 等跨层契约。

## 4. Agent 架构

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
Task Confirmation
 ↓
Task Queue
```

Agent 不应直接产生不可控的鼠标点击指令作为生产执行结果。生产任务应解析到已注册的 Function 和 Workflow。

## 5. Workflow Engine

Workflow 由结构化 Step 组成，例如：

```json
{
  "version": 3,
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

## 6. Browser Engine

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

## 7. Task Engine

任务引擎负责：

- 状态机
- 队列
- 并发控制
- 超时
- CancellationToken
- RetryPolicy
- TaskItem
- Worker Lease
- 断点恢复

长任务必须采用持久化状态，而不能只依赖内存状态。

## 8. Worker

推荐 Worker 使用租约机制：

```text
Queued
 ↓
Worker Acquire Lease
 ↓
Running
 ↓
Heartbeat
 ↓
Completed / Failed
```

Worker 意外退出后，超过 LeaseTimeout 的任务可以重新进入队列。

## 9. 数据层

核心实体：

```text
User
Role
Organization
City
BusinessSystem
BusinessFunction
Workflow
WorkflowVersion
Credential
Task
TaskItem
TaskExecution
TaskArtifact
TaskResult
AuditLog
```

数据库实现应通过 Repository/Unit of Work 或等价抽象隔离，避免 Domain 直接依赖数据库。

## 10. 外部系统适配器

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

## 11. LLM Provider

通过统一接口支持不同模型：

```text
ILLMProvider
├── OpenAICompatible
├── LocalModel
└── OtherProvider
```

Agent 输出必须经过 JSON Schema/Contract 验证，再进入 TaskPlan。

## 12. 事件

推荐领域事件：

```text
TaskCreated
TaskPlanned
PermissionDenied
TaskQueued
TaskStarted
TaskPaused
HumanInterventionRequested
TaskResumed
TaskItemCompleted
TaskItemFailed
TaskCompleted
TaskFailed
WorkflowVersionPublished
```

后续可以使用消息队列实现跨进程任务通知和事件驱动。

## 13. 可观测性

统一记录：

- TraceId
- TaskId
- ExecutionId
- WorkflowId
- WorkflowVersion
- UserId
- WorkerId
- StepId
- Duration
- Result

敏感数据不得直接进入日志。

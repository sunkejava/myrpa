# AgentRPA 开发实施计划与实时进度

> 本文档是当前开发进度的唯一阶段性参考。架构发生变化时，先同步本文档，再推进代码实现。

## 总体阶段

| 阶段 | 主题 | 状态 |
|---|---|---|
| Phase 0 | 基础工程与统一构建 | 🟡 部分完成 |
| Phase 1 | 平台基础 + 一托 N 执行节点 | 🟡 架构骨架完成，工程化建设中 |
| Phase 2 | Workflow | ⚪ 未开始 |
| Phase 3 | RPA Engine / NodeAgent 执行 | ⚪ 未开始 |
| Phase 4 | Execution Scheduler 生产化 | 🟡 调度骨架已完成，持久化/通信待完成 |
| Phase 5 | Agent | ⚪ 未开始 |
| Phase 6 | 批量业务 | ⚪ 未开始 |
| Phase 7 | 人工介入与外部集成 | 🟡 架构设计完成，Provider 待实现 |
| Phase 8 | 运营中心 | ⚪ 未开始 |
| Phase 9 | 扩展能力 | ⚪ 未开始 |

---

## Phase 0：基础工程

### 目标

建立 .NET 10 DDD/Clean Architecture、Vue 3 前端、统一配置和构建基础。

### 已完成

- [x] .NET 10 Domain / Application / Infrastructure / API / Worker 分层目录
- [x] Contracts 独立项目
- [x] NodeAgent 独立执行项目
- [x] 基础领域实体 `Entity`
- [x] API 基础启动代码

### 待完成

- [ ] `.sln/.slnx` Solution
- [ ] Directory.Build.props / 统一编译规则
- [ ] NuGet 包版本统一
- [ ] Swagger/OpenAPI 构建依赖完善
- [ ] 统一异常处理
- [ ] JWT/Identity
- [ ] EF Core 数据库与迁移
- [ ] Vue 3 + TypeScript 工程
- [ ] 统一 API Client
- [ ] i18n
- [ ] Theme Design Token
- [ ] 基础 Layout
- [ ] 通用 SearchForm/DataTable/FormDialog
- [ ] CI 构建与测试

---

## Phase 1：平台基础 + 一托 N 执行节点

### 架构调整

本阶段已将原来的“服务端直接执行 RPA”调整为：

```text
AgentRPA Server
   │
   ├── Agent
   ├── Permission Engine
   ├── Task Queue
   ├── Scheduler
   └── Node Registry
          │
          ├── Windows VM NodeAgent
          ├── Windows Physical NodeAgent
          ├── Windows CloudDesktop NodeAgent
          └── Linux NodeAgent
                    │
                 Worker × N
```

Server 是控制面，NodeAgent 是执行面；Server 不直接控制 Windows 桌面。

### 已完成

- [x] 用户需求与总体架构重新定义
- [x] 城市 → 系统 → 功能 → Action 权限模型设计
- [x] `ExecutionNode` 领域模型骨架
- [x] `NodeKind` / `OsPlatform` 分离建模
- [x] `NodePool` 领域模型骨架
- [x] `NodeCapability` 领域模型骨架
- [x] `WorkerSlot` 领域模型骨架
- [x] `ExecutionRequirement` 调度要求模型
- [x] WorkflowVersion 对业务系统执行要求的收紧规则
- [x] Scheduler 硬约束过滤
- [x] Scheduler 软评分骨架
- [x] `ExecutionCommand` / `ExecutionProgress` 契约
- [x] NodeAgent 独立项目骨架
- [x] Node 注册/心跳 API 初始契约
- [x] UKey、验证码、二维码、人脸、通知 Provider 抽象设计
- [x] 数据模型文档增加 Node/Lease/ResourceLock/Dispatch 等实体

### 当前进行中

- [ ] Solution 与统一构建配置
- [ ] EF Core 持久化模型
- [ ] Node Registry 持久化实现
- [ ] Node 身份认证
- [ ] 节点注册审批
- [ ] WorkerSlot 持久化
- [ ] NodeLease 持久化
- [ ] ResourceLock 持久化
- [ ] 节点在线/离线状态自动判定
- [ ] SignalR Server ↔ NodeAgent
- [ ] ExecutionDispatch 实际派发

### Phase 1 验收标准

1. 一台 Server 可以同时管理多台异构执行节点。
2. 可以区分 Physical / VirtualMachine / CloudDesktop / Container。
3. 可以区分 Windows / Linux / macOS 等 OS。
4. 可以查看节点能力、网络区域、NodePool 和 WorkerSlot。
5. Windows-only 任务不会派发到 Linux。
6. UKey 任务只会选择拥有目标 UKey 的节点。
7. 节点离线后不会继续接收新任务。
8. 节点恢复后可以重新加入调度池。

---

## Phase 2：Workflow

### 目标

将业务系统自动化过程定义为可版本化、可测试、可发布的确定性 Workflow。

- [ ] Workflow CRUD
- [ ] Workflow Version
- [ ] Step Schema
- [ ] 子流程
- [ ] Locator 管理
- [ ] 流程测试
- [ ] 发布/停用
- [ ] BusinessSystem 默认 ExecutionRequirement
- [ ] WorkflowVersion ExecutionRequirement
- [ ] Task 级 ExecutionRequirement 收紧规则

验收标准：可以配置“该系统必须 Windows”“该 Workflow 必须 Windows + DesktopUI”“该任务必须指定 UKey”等约束。

---

## Phase 3：RPA Engine + NodeAgent

- [ ] Browser Worker
- [ ] Playwright 生命周期管理
- [ ] Desktop UI Worker
- [ ] Step Runner
- [ ] Retry Policy
- [ ] Screenshot
- [ ] Download/Upload
- [ ] Execution Log
- [ ] Windows NodeAgent 本地执行能力
- [ ] Worker 生命周期管理
- [ ] NodeAgent 接收 ExecutionCommand
- [ ] ExecutionProgress 回传
- [ ] Cancel / Pause / Resume
- [ ] NodeAgent 本地 Provider 注册

验收标准：服务端派发一个 Workflow，指定 WorkerSlot 后，NodeAgent 能执行并持续上报状态，服务端可取消并回收资源。

---

## Phase 4：Execution Scheduler 生产化

调度骨架已经完成，生产化重点为资源一致性和故障恢复。

- [x] ExecutionRequirement
- [x] Node Capability Matching 基础逻辑
- [x] NodePool 基础模型
- [x] Required/Excluded Node 基础模型
- [x] Network Zone 基础模型
- [x] UKey/Hardware ID 基础约束模型
- [x] Worker Capacity 基础约束模型
- [x] Node Health 状态模型
- [ ] Node Affinity 完整评分策略
- [ ] Preferred Node
- [ ] Credential Affinity
- [ ] DB 原子 Lease
- [ ] Lease Renewal / Expiration Recovery
- [ ] UKey ResourceLock 原子抢占
- [ ] 调度幂等
- [ ] 节点故障后的安全重调度
- [ ] Permission-filtered candidate set
- [ ] 大规模节点调度性能优化

验收标准：100 个 TaskItem 可以按资源约束分配到多台 Node 并行执行，节点故障时只安全恢复可重试项。

---

## Phase 5：Agent

- [ ] Chat
- [ ] Intent Parser
- [ ] Resource Resolver
- [ ] TaskPlan
- [ ] Permission Pre-check
- [ ] Confirmation
- [ ] Workflow Selection
- [ ] ExecutionRequirement 解析
- [ ] 任务参数结构化
- [ ] 自然语言生成执行摘要

---

## Phase 6：批量业务

- [ ] Excel / CSV
- [ ] TaskItem
- [ ] 每条数据独立状态
- [ ] 并发控制
- [ ] 跨 Node 并发
- [ ] 失败项重试
- [ ] 断点续跑
- [ ] 结果导出
- [ ] Artifact 管理

---

## Phase 7：人工介入与集成能力

### 架构设计已完成

- [x] `IHardwareCredentialProvider`
- [x] `ICaptchaProvider`
- [x] `INotificationProvider`
- [x] UKey Provider 设计
- [x] Captcha Provider 设计
- [x] QRCode Human Intervention 设计
- [x] Face/RealName Human Intervention 设计
- [x] Notification Center 设计

### 待实现

- [ ] Windows UKey Discovery / Status / Operation Provider
- [ ] 第三方 Captcha Provider Adapter
- [ ] Captcha 超时/重试/故障切换
- [ ] QR 临时安全入口
- [ ] HumanIntervention 状态机
- [ ] 用户扫码后恢复 Execution
- [ ] 人脸/实名认证人工接管
- [ ] Email Provider
- [ ] 钉钉 Provider
- [ ] 微信 Provider
- [ ] 企业微信 Provider
- [ ] WebSocket/站内通知 Provider
- [ ] SMS/Webhook 扩展

---

## Phase 8：运营中心

- [ ] 机器人资源池
- [ ] Node 状态监控
- [ ] NodePool 管理
- [ ] Node Capability 管理
- [ ] WorkerSlot 管理
- [ ] 调度实时监控
- [ ] ExecutionDispatch 时间线
- [ ] 人工介入中心
- [ ] UKey/硬件资源中心
- [ ] 通知中心
- [ ] 执行统计
- [ ] 失败分析
- [ ] 审计查询
- [ ] 节点资源监控

---

## Phase 9：扩展能力

- [ ] 更多城市
- [ ] 更多业务系统 Adapter
- [ ] 多 LLM Provider
- [ ] 本地模型
- [ ] 云模型
- [ ] 对象存储
- [ ] Linux Node
- [ ] Windows 云桌面
- [ ] 分布式 Worker
- [ ] 自动扩缩容

---

## 当前代码完成度说明

当前完成的是**架构与骨架代码**，不是生产可运行版本：

- Domain 已具备 Node / Pool / Capability / WorkerSlot 基础模型。
- Application 已具备 ExecutionRequirement、调度接口和基础 Scheduler。
- Contracts 已具备 Node 注册、心跳、ExecutionCommand、ExecutionProgress。
- API 已具备 Node 注册/心跳初始入口，但仍是契约骨架。
- NodeAgent 已具备独立进程骨架，但尚未连接 Server。
- Infrastructure 尚未完成 EF Core、Node Registry、Lease 和 SignalR 实现。

由于此前开发环境没有 .NET SDK，现阶段不能把仓库代码视为已经通过本地编译验证；后续必须通过 CI 建立 `restore → build → test → publish` 验证链路。

## 开发规则

1. 每完成一个阶段/子阶段，立即同步本文件的 `[x]/[ ]` 状态。
2. 架构发生变化时，README、architecture、data-model、api-design 和本文件必须同步更新。
3. 任何“已完成”项必须有对应代码、文档或测试证据。
4. 每个 Phase 尽量保持可编译、可运行、可回滚。
5. 业务页面优先复用公共组件，禁止大量复制 CRUD 逻辑。
6. 所有 API、领域实体、Application 服务及复杂前端逻辑提供中文注释/说明。

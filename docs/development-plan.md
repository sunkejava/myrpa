# AgentRPA 开发实施计划与实时进度

> 本文档是当前开发进度的唯一阶段性参考。架构发生变化时，先同步本文档，再推进代码实现。

## 总体阶段

| 阶段 | 主题 | 状态 |
|---|---|---|
| Phase 0 | 基础工程与统一构建 | 🟢 基础工程已完成，认证/前端待建设 |
| Phase 1 | 平台基础 + 一托 N 执行节点 | 🟡 节点持久化已落地，通信/租约待建设 |
| Phase 2 | Workflow | ⚪ 未开始 |
| Phase 3 | RPA Engine / NodeAgent 执行 | ⚪ 未开始 |
| Phase 4 | Execution Scheduler 生产化 | 🟡 调度骨架已完成，资源一致性待完成 |
| Phase 5 | Agent | ⚪ 未开始 |
| Phase 6 | 批量业务 | ⚪ 未开始 |
| Phase 7 | 人工介入与外部集成 | 🟡 架构设计完成，Provider 待实现 |
| Phase 8 | 运营中心 | ⚪ 未开始 |
| Phase 9 | 扩展能力 | ⚪ 未开始 |

## Phase 0：基础工程

### 已完成

- [x] .NET 10 Domain / Application / Infrastructure / API / Worker 分层目录
- [x] Contracts 独立项目
- [x] NodeAgent 独立执行项目
- [x] 基础领域实体 `Entity`
- [x] `AgentRPA.sln` 统一 Solution
- [x] `Directory.Build.props` 统一 .NET 10 编译基础配置
- [x] API Swagger 构建依赖
- [x] NodeAgent Hosting 依赖
- [x] EF Core 10 + SQLite 基础依赖
- [x] GitHub Actions 后端 restore/build 验证链路

### 待完成

- [ ] NuGet 包版本集中管理
- [ ] 统一异常处理 / ProblemDetails
- [ ] JWT/Identity
- [ ] EF Core 正式 Migration 流程
- [ ] Vue 3 + TypeScript 工程
- [ ] 统一 API Client
- [ ] i18n / Theme Design Token / 基础 Layout
- [ ] 通用 SearchForm/DataTable/FormDialog
- [ ] 自动化测试项目
- [ ] CI test/publish 阶段

## Phase 1：平台基础 + 一托 N 执行节点

### 核心架构

```text
AgentRPA Server
   │
   ├── Agent / Permission Engine / Task Queue / Scheduler
   └── Node Registry
          ├── Windows VM NodeAgent
          ├── Windows Physical NodeAgent
          ├── Windows CloudDesktop NodeAgent
          └── Linux NodeAgent
                    │
                 Worker × N
```

Server 是控制面，NodeAgent 是执行面；Server 不直接控制 Windows 桌面。

### 已完成

- [x] 城市 → 系统 → 功能 → Action 权限模型设计
- [x] `ExecutionNode` / `NodeKind` / `OsPlatform` / `NodePool`
- [x] `NodeCapability` / `WorkerSlot`
- [x] `ExecutionRequirement` 与 WorkflowVersion 收紧规则
- [x] Scheduler 硬约束过滤与软评分骨架
- [x] `ExecutionCommand` / `ExecutionProgress` 契约
- [x] NodeAgent 独立项目骨架
- [x] UKey、验证码、二维码、人脸、通知 Provider 抽象设计
- [x] 数据模型文档增加 Node/Lease/ResourceLock/Dispatch 等实体
- [x] EF Core DbContext
- [x] City / BusinessSystem / BusinessFunction 基础持久化映射
- [x] ExecutionNode / NodePool / NodeCapability / WorkerSlot 持久化映射
- [x] Node Registry 应用服务接口与 EF Core 实现
- [x] AgentKey 稳定节点身份字段
- [x] 注册 API / 心跳 API 接入持久化 Registry
- [x] 按最近心跳筛选可调度节点
- [x] WorkerSlot 与节点能力注册/刷新

### 当前进行中

- [ ] 节点身份认证：API Key / mTLS / 证书
- [ ] 节点注册审批与 Disabled 状态控制
- [ ] NodePool 管理 API
- [ ] Node/WorkerSlot 查询 API
- [ ] 节点离线状态自动落库
- [ ] NodeLease / ResourceLock 持久化
- [ ] SignalR Server ↔ NodeAgent
- [ ] ExecutionDispatch 实际派发
- [ ] WorkerSlot/硬件 Lease 原子抢占
- [ ] Scheduler 接入真实 Lease

### Phase 1 验收标准

1. 一台 Server 可以同时管理多台异构执行节点。
2. 可以区分 Physical / VirtualMachine / CloudDesktop / Container。
3. 可以区分 Windows / Linux / macOS 等 OS。
4. 可以查看节点能力、网络区域、NodePool 和 WorkerSlot。
5. Windows-only 任务不会派发到 Linux。
6. UKey 任务只会选择拥有目标 UKey 的节点。
7. 节点离线后不会继续接收新任务。
8. 节点恢复后可以重新加入调度池。

## Phase 2：Workflow

- [ ] Workflow CRUD / Version / Step Schema
- [ ] 子流程 / Locator 管理 / 流程测试
- [ ] 发布/停用
- [ ] BusinessSystem / WorkflowVersion / Task ExecutionRequirement

## Phase 3：RPA Engine + NodeAgent

- [ ] Browser Worker / Playwright
- [ ] Desktop UI Worker / Step Runner
- [ ] Retry / Screenshot / Download / Upload
- [ ] Execution Log
- [ ] Worker 生命周期管理
- [ ] ExecutionCommand 接收与 ExecutionProgress 回传
- [ ] Cancel / Pause / Resume
- [ ] NodeAgent 本地 Provider 注册

## Phase 4：Execution Scheduler 生产化

- [x] ExecutionRequirement / Capability Matching / NodePool
- [x] Required/Excluded Node / Network Zone / Hardware ID
- [x] Worker Capacity / Node Health
- [ ] Node Affinity / Preferred Node / Credential Affinity
- [ ] DB 原子 Lease / Renewal / Expiration Recovery
- [ ] UKey ResourceLock 原子抢占
- [ ] 调度幂等 / 故障安全重调度
- [ ] Permission-filtered candidate set
- [ ] 大规模节点调度性能优化

## Phase 5：Agent

- [ ] Chat / Intent Parser / Resource Resolver / TaskPlan
- [ ] Permission Pre-check / Confirmation
- [ ] Workflow Selection / ExecutionRequirement 解析
- [ ] 任务参数结构化 / 执行摘要

## Phase 6：批量业务

- [ ] Excel / CSV / TaskItem
- [ ] 每条数据独立状态 / 并发控制 / 跨 Node 并发
- [ ] 失败项重试 / 断点续跑 / 结果导出 / Artifact

## Phase 7：人工介入与集成能力

### 架构设计已完成

- [x] Hardware / Captcha / Notification Provider
- [x] UKey / Captcha / QRCode / Face Human Intervention
- [x] Notification Center

### 待实现

- [ ] Windows UKey Provider
- [ ] 第三方 Captcha Adapter 与故障切换
- [ ] QR 临时安全入口 / HumanIntervention 状态机
- [ ] 用户扫码后恢复 Execution
- [ ] 人脸/实名认证人工接管
- [ ] Email / 钉钉 / 微信 / 企业微信 / WebSocket Provider
- [ ] SMS/Webhook 扩展

## Phase 8：运营中心

- [ ] 机器人资源池 / Node 状态监控 / NodePool 管理
- [ ] Node Capability / WorkerSlot 管理
- [ ] 调度实时监控 / ExecutionDispatch 时间线
- [ ] 人工介入中心 / UKey 硬件资源中心 / 通知中心
- [ ] 执行统计 / 失败分析 / 审计查询 / 节点资源监控

## Phase 9：扩展能力

- [ ] 更多城市 / 业务系统 Adapter
- [ ] 多 LLM Provider / 本地模型 / 云模型
- [ ] 对象存储 / Linux Node / Windows 云桌面
- [ ] 分布式 Worker / 自动扩缩容

## 当前代码完成度说明

当前已经从“纯架构骨架”进入**可持久化的平台基础实现阶段**：Domain、Application、Contracts 已具备核心模型；Infrastructure 已接入 EF Core 10 + SQLite 并实现 Node Registry；API 注册/心跳已经连接持久化 Registry；NodeAgent 仍未连接 Server；Lease、ResourceLock、SignalR、ExecutionDispatch 尚未实现。

此前开发环境没有 .NET SDK，不能把本地编译作为验证依据；已加入 GitHub Actions `restore → build` 基础链路，后续以 CI 结果为准继续补齐 test/publish。

## 开发规则

1. 每完成一个阶段/子阶段，立即同步本文件的 `[x]/[ ]` 状态。
2. 架构发生变化时，README、architecture、data-model、api-design 和本文件必须同步更新。
3. 任何“已完成”项必须有对应代码、文档或测试证据。
4. 每个 Phase 尽量保持可编译、可运行、可回滚。
5. 业务页面优先复用公共组件，禁止大量复制 CRUD 逻辑。
6. 所有 API、领域实体、Application 服务及复杂前端逻辑提供中文注释/说明。

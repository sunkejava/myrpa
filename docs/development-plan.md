# AgentRPA 开发实施计划与实时进度

> 架构发生变化时先同步本文档，再推进代码；本文件作为阶段进度基准。

## 总体进度

| 阶段 | 主题 | 状态 |
|---|---|---|
| Phase 0 | 基础工程与统一构建 | 🟢 基础工程完成，认证/前端待建设 |
| Phase 1 | 平台基础 + 一托 N 执行节点 | 🟢 节点注册/心跳/健康监测/持久化 Lease 已完成，认证/Dispatch 待完成 |
| Phase 2 | Workflow | ⚪ 未开始 |
| Phase 3 | RPA Engine / NodeAgent | 🟡 NodeAgent 通信骨架已开始，RPA Engine 未开始 |
| Phase 4 | Scheduler 生产化 | 🟡 调度骨架 + 持久化 Worker Lease 已完成，分布式原子 Lease 待完成 |
| Phase 5 | Agent | ⚪ 未开始 |
| Phase 6 | 批量业务 | ⚪ 未开始 |
| Phase 7 | 人工介入与外部集成 | 🟡 架构设计完成，Provider 待实现 |
| Phase 8 | 运营中心 | ⚪ 未开始 |
| Phase 9 | 扩展能力 | ⚪ 未开始 |

## Phase 0：基础工程

### 已完成
- [x] .NET 10 Domain / Application / Infrastructure / API / Worker
- [x] Contracts / NodeAgent 独立项目
- [x] `AgentRPA.sln`
- [x] `Directory.Build.props`
- [x] Swagger / OpenAPI 构建依赖
- [x] EF Core 10 + SQLite
- [x] NodeAgent Hosting + SignalR Client 依赖
- [x] GitHub Actions `restore → build`

### 待完成
- [ ] NuGet 集中版本管理
- [ ] ProblemDetails / 全局异常
- [ ] JWT / Identity
- [ ] EF Core Migration
- [ ] Vue3 + TypeScript / API Client / i18n / Theme
- [ ] 自动化测试与 CI test/publish

## Phase 1：平台基础 + 一托 N 执行节点

### 核心架构

```text
AgentRPA Server
 ├─ Agent / Permission / Task Queue / Scheduler
 └─ Node Registry
      ├─ Windows VM NodeAgent
      ├─ Windows Physical NodeAgent
      ├─ Windows CloudDesktop NodeAgent
      └─ Linux NodeAgent
             └─ Worker × N
```

### 已完成
- [x] `ExecutionNode` / `NodeKind` / `OsPlatform`
- [x] `NodePool` / `NodeCapability` / `WorkerSlot`
- [x] 稳定 `AgentKey` 节点身份
- [x] `ExecutionRequirement`、Scheduler 硬过滤/软评分
- [x] Node 注册/心跳 Contracts 与 API
- [x] EF Core DbContext 与节点相关映射
- [x] Node Registry 应用接口 + EF Core 实现
- [x] WorkerSlot / Capability 注册与刷新
- [x] 最近心跳节点筛选
- [x] SignalR Hub / typed contract
- [x] NodeAgent 自动重连、状态连接骨架
- [x] NodeAgent 注册 → SignalR Connect 基础链路
- [x] `NodeLease` / `ResourceLock` 领域模型与 EF Core 持久化映射
- [x] Scheduler 的 WorkerSlot 持久化 Lease 获取/释放
- [x] Node Agent 心跳超时自动标记 Offline

### 当前进行中
- [ ] 节点 API Key / mTLS / 证书认证
- [ ] 节点注册审批 / Disabled 控制
- [ ] NodePool / Node / WorkerSlot 查询管理 API
- [ ] SignalR 心跳 / ACK / Progress 完整协议
- [ ] ExecutionDispatch 实际派发
- [ ] Scheduler 接入真实 Lease 的分布式原子化

### 验收标准
1. Server 同时管理多台异构 Node。
2. 区分 Physical / VM / CloudDesktop / Container。
3. 区分 Windows / Linux / macOS。
4. 查看 Node Capability / NetworkZone / NodePool / WorkerSlot。
5. Windows-only 任务绝不派发 Linux。
6. UKey 任务只能选择目标 UKey 所在节点。
7. 离线节点不接收新任务。
8. 节点恢复后自动重新进入调度候选。

## Phase 2：Workflow
- [ ] Workflow CRUD / Version / Step Schema
- [ ] Locator / 子流程 / 流程测试
- [ ] 发布/停用
- [ ] BusinessSystem / WorkflowVersion / Task ExecutionRequirement

## Phase 3：RPA Engine + NodeAgent
- [ ] Playwright Browser Worker
- [ ] Windows Desktop UI Worker
- [ ] Step Runner / Retry / Screenshot / Upload / Download
- [ ] Execution Log
- [ ] Worker 生命周期
- [ ] ExecutionCommand 真正执行
- [ ] ExecutionProgress / Cancel / Pause / Resume
- [ ] UKey / Captcha / File Provider 接入

## Phase 4：Scheduler 生产化
- [x] ExecutionRequirement / Capability Matching / NodePool
- [x] Required/Excluded Node / NetworkZone / Hardware ID
- [x] Worker Capacity / Node Health
- [x] 持久化 Worker Slot Lease 获取/释放
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
- [ ] 参数结构化 / 执行摘要

## Phase 6：批量业务
- [ ] Excel / CSV / TaskItem
- [ ] 每项独立状态 / 并发控制 / 跨 Node 并发
- [ ] 失败项重试 / 断点续跑 / 结果导出 / Artifact

## Phase 7：人工介入与集成
### 已完成架构设计
- [x] Hardware / Captcha / Notification Provider
- [x] UKey / Captcha / QR / Face Human Intervention
- [x] Notification Center
### 待实现
- [ ] Windows UKey Provider
- [ ] 第三方 Captcha Adapter / 故障切换
- [ ] QR 临时安全入口 / 人工介入状态机
- [ ] 扫码后恢复 Execution / 人脸实名认证接管
- [ ] Email / 钉钉 / 微信 / 企业微信 / WebSocket
- [ ] SMS / Webhook

## Phase 8：运营中心
- [ ] Node / NodePool / Capability / WorkerSlot 管理
- [ ] 调度监控 / ExecutionDispatch 时间线
- [ ] 人工介入 / UKey / 通知中心
- [ ] 执行统计 / 失败分析 / 审计 / 资源监控

## Phase 9：扩展能力
- [ ] 更多城市 / 业务系统 Adapter
- [ ] 多 LLM Provider / 本地模型 / 云模型
- [ ] 对象存储 / Linux Node / Windows 云桌面
- [ ] 分布式 Worker / 自动扩缩容

## 当前代码完成度

当前已经从架构骨架进入**可持久化 + 节点实时通信 + 基础资源租约阶段**：

- Domain：Node / Pool / Capability / WorkerSlot + AgentKey + NodeLease / ResourceLock 已具备。
- Application：ExecutionRequirement、Scheduler 接口、Node Registry 契约、节点健康检查契约已具备。
- Contracts：注册、心跳、ExecutionCommand、ExecutionProgress、SignalR typed contract 已具备。
- Infrastructure：EF Core 10 + SQLite、Node Registry、WorkerSlot Lease 已落地。
- API：Node 注册/心跳、SignalR Hub、NodeHealthMonitor 已接入。
- NodeAgent：已具备注册、SignalR 自动重连和命令接收骨架。
- 尚未生产化：Node 认证、审批、分布式原子 Lease、ResourceLock 抢占、真实 ExecutionDispatch、RPA Engine。

## 开发规则

1. 完成子阶段立即同步 `[x]/[ ]`。
2. 架构变化同步 README / architecture / data-model / api-design / 本文档。
3. “已完成”必须有代码、文档或测试证据。
4. 保持每阶段可编译、可运行、可回滚。
5. 前端优先复用公共组件，禁止复制 CRUD 逻辑。
6. API、领域、Application 及复杂前端逻辑提供中文注释/说明。
7. **每次代码或文档推送后必须检查 GitHub Actions；Build 失败立即定位、修复并再次轮巡，直到最新提交 Build 成功后再进入下一项任务。**

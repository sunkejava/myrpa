# AgentRPA 开发实施计划与实时进度

> 架构发生变化时先同步本文档，再推进代码；本文件作为阶段进度基准。

## 总体进度

| 阶段 | 主题 | 状态 |
|---|---|---|
| Phase 0 | 基础工程与统一构建 | 🟡 基础工程、ProblemDetails 已完成，认证/迁移/前端/测试待建设 |
| Phase 1 | 平台基础 + 一托 N 执行节点 | 🟢 注册/心跳/健康监测/Lease/管理/Dispatch/Progress 主链路已落地，安全认证与分布式原子化待完成 |
| Phase 2 | Workflow | 🟢 Workflow / Version / Step / Task 基础模型与 API 已完成，流程设计器/测试待完成 |
| Phase 3 | RPA Engine / NodeAgent | 🟡 NodeAgent 通信、命令接收、Progress 回传协议已具备，真实浏览器/桌面执行引擎待建设 |
| Phase 4 | Scheduler 生产化 | 🟡 Capability Matching、NodePool、Worker Lease 已完成，分布式原子 Lease / 重调度待完成 |
| Phase 5 | Agent | ⚪ 未开始 |
| Phase 6 | 批量业务 | 🟡 Task / TaskItem 基础能力已完成，Excel/CSV/并发/断点续跑待完成 |
| Phase 7 | 人工介入与外部集成 | 🟡 HumanIntervention 生命周期 API 已完成，UKey/Captcha/通知 Provider 待接入 |
| Phase 8 | 运营中心 | 🟡 Node 管理、状态控制、人工介入 API 已开始，完整运营 UI/统计/审计待完成 |
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
- [x] ASP.NET Core ProblemDetails / 全局异常处理

### 待完成
- [ ] NuGet 集中版本管理
- [ ] JWT / Identity
- [ ] EF Core Migration
- [ ] Vue3 + TypeScript / API Client / i18n / Theme
- [ ] 自动化测试与 CI test/publish

## Phase 1：平台基础 + 一托 N 执行节点

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
- [x] SignalR Heartbeat ACK
- [x] `NodeLease` / `ResourceLock` 领域模型与 EF Core 持久化映射
- [x] Scheduler 的 WorkerSlot 持久化 Lease 获取/释放
- [x] Node Agent 心跳超时自动标记 Offline
- [x] Node / WorkerSlot / NodePool 管理查询与节点状态控制
- [x] Node Drain / Disabled 控制
- [x] ExecutionDispatch → NodeAgent SignalR ExecuteAsync
- [x] ExecutionProgress → Execution / TaskItem 状态持久化

### 待完成
- [ ] 节点 API Key / mTLS / 证书认证
- [ ] 正式注册审批状态机
- [ ] Scheduler 接入数据库原子 Lease / Renewal
- [ ] UKey / Hardware ResourceLock 原子抢占
- [ ] Dispatch 幂等与故障安全重调度

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
- [x] Workflow CRUD / Version / Step Schema 基础 API
- [x] WorkflowVersion 发布数据模型
- [x] Task / TaskItem / Execution 基础模型
- [x] Locator / 子流程数据结构预留
- [ ] 可视化 Workflow Designer
- [ ] 流程测试与单步调试
- [ ] 完整发布/停用策略
- [ ] BusinessSystem / WorkflowVersion / Task ExecutionRequirement 完整约束

## Phase 3：RPA Engine + NodeAgent
- [ ] Playwright Browser Worker
- [ ] Windows Desktop UI Worker
- [ ] Step Runner / Retry / Screenshot / Upload / Download
- [ ] Execution Log
- [ ] Worker 生命周期
- [x] ExecutionCommand 通信协议与 NodeAgent 接收骨架
- [x] ExecutionProgress Server 持久化入口
- [ ] ExecutionCommand 真正执行
- [ ] Cancel / Pause / Resume 真正执行
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
- [x] Task / TaskItem 基础模型
- [x] 每项独立状态 / RetryCount
- [ ] Excel / CSV Import
- [ ] 并发控制 / 跨 Node 并发
- [ ] 失败项重试 / 断点续跑
- [ ] 结果导出 / Artifact

## Phase 7：人工介入与集成
### 已完成
- [x] Hardware / Captcha / Notification Provider 抽象
- [x] UKey / Captcha / QR / Face Human Intervention 数据模型设计
- [x] HumanIntervention 持久化
- [x] HumanIntervention 创建 / 查询 / 完成 / 取消 API
- [x] Notification Center 架构设计
### 待实现
- [ ] Windows UKey Provider
- [ ] 第三方 Captcha Adapter / 故障切换
- [ ] QR 临时安全入口 / 人工介入状态机
- [ ] 扫码后恢复 Execution / 人脸实名认证接管
- [ ] Email / 钉钉 / 微信 / 企业微信 / WebSocket
- [ ] SMS / Webhook

## Phase 8：运营中心
- [x] Node 状态管理 API
- [x] Node Drain / Disable
- [x] Human Intervention 管理 API
- [ ] Node / NodePool / Capability / WorkerSlot 完整管理 UI
- [ ] 调度监控 / ExecutionDispatch 时间线
- [ ] 人工介入 / UKey / 通知中心 UI
- [ ] 执行统计 / 失败分析 / 审计 / 资源监控

## Phase 9：扩展能力
- [ ] 更多城市 / 业务系统 Adapter
- [ ] 多 LLM Provider / 本地模型 / 云模型
- [ ] 对象存储 / Linux Node / Windows 云桌面
- [ ] 分布式 Worker / 自动扩缩容

## 当前代码完成度

当前已经从架构骨架进入**可持久化 + 节点实时通信 + 执行派发 + 人工介入控制阶段**：

- Domain：Node / Pool / Capability / WorkerSlot + AgentKey + NodeLease / ResourceLock + Task / Execution + HumanIntervention 已具备。
- Application：ExecutionRequirement、Scheduler、Node Registry、节点健康检查契约已具备。
- Contracts：注册、心跳、Heartbeat ACK、ExecutionCommand、ExecutionProgress、HumanIntervention API 契约已具备。
- Infrastructure：EF Core 10 + SQLite、Node Registry、WorkerSlot Lease、HumanIntervention 持久化已落地。
- API：Node 注册/心跳/状态管理、SignalR Hub、ExecutionDispatch、ExecutionProgress、HumanIntervention、ProblemDetails 已接入。
- NodeAgent：已具备注册、SignalR 自动重连、命令接收骨架。
- 尚未生产化：节点认证、完整审批、分布式原子 Lease、真实 RPA Engine、Agent、前端、通知/验证码/UKey Provider。

## 开发规则

1. 完成子阶段立即同步 `[x]/[ ]`。
2. 架构变化同步 README / architecture / data-model / api-design / 本文档。
3. “已完成”必须有代码、文档或测试证据。
4. 保持每阶段可编译、可运行、可回滚。
5. 前端优先复用公共组件，禁止复制 CRUD 逻辑。
6. API、领域、Application 及复杂前端逻辑提供中文注释/说明。
7. **每次代码或文档推送后必须检查 GitHub Actions；Build 失败立即定位、修复并再次轮巡，直到最新提交 Build 成功后再进入下一项任务。**

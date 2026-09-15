# AgentRPA 开发实施计划与实时进度

> 架构发生变化时先同步本文档，再推进代码；本文件作为阶段进度基准。

## 总体进度

| 阶段 | 主题 | 状态 |
|---|---|---|
| Phase 0 | 基础工程与统一构建 | 🟢 基础工程、ProblemDetails、前后端独立构建流水线已完成；认证/迁移/测试待建设 |
| Phase 1 | 平台基础 + 一托 N 执行节点 | 🟢 注册认证、审批状态、节点健康、数据库乐观并发 Lease、节点池/WorkerSlot 管理已落地；ResourceLock/Dispatch 幂等待完成 |
| Phase 2 | Workflow | 🟢 Workflow / Version / Step / Task API 与前端 Designer 已具备；在线调试与完整发布策略待完成 |
| Phase 3 | RPA Engine / NodeAgent | 🟢 Playwright 确定性 Step Runner 已支持浏览器常用步骤；Desktop、HumanTask 恢复与完整执行控制待完成 |
| Phase 4 | Scheduler 生产化 | 🟢 Capability Matching、NodePool、Worker Lease、数据库乐观并发抢占已完成；ResourceLock/故障重调度待完成 |
| Phase 5 | Agent | ⚪ 未开始 |
| Phase 6 | 批量业务 | 🟢 CSV/XLSX 导入、TaskItem 独立状态/重试已完成；跨 Node 并发、断点续跑、结果 Artifact 待完成 |
| Phase 7 | 人工介入与外部集成 | 🟢 HumanIntervention 生命周期、Captcha HTTP Adapter、Webhook/Email、Windows 证书型 UKey Provider 已具备；QR 恢复链路与厂商 UKey SDK 待完成 |
| Phase 8 | 运营中心 | 🟢 Node/Pool/Worker 管理 API、人工介入 API、审计模型/API、控制中心前端骨架已完成；完整实时运营 UI/指标待完成 |
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
- [x] GitHub Actions 后端 `restore → build`
- [x] GitHub Actions 前端独立 `npm install → vue-tsc → vite build`
- [x] ASP.NET Core ProblemDetails / 全局异常处理
- [x] Vue3 + TypeScript + Vite + Theme / Dashboard / 公共组件基础结构

### 待完成
- [ ] NuGet 集中版本管理
- [ ] JWT / Identity
- [ ] EF Core Migration
- [ ] 完整 API Client / i18n / Theme Settings
- [ ] 自动化测试与 CI test/publish

## Phase 1：平台基础 + 一托 N 执行节点

### 已完成
- [x] `ExecutionNode` / `NodeKind` / `OsPlatform`
- [x] `NodePool` / `NodeCapability` / `WorkerSlot`
- [x] 稳定 `AgentKey` 节点身份
- [x] 首次注册 Bootstrap Key + AgentKey 心跳认证
- [x] PendingApproval → Online 审批状态
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
- [x] WorkerSlot ConcurrencyStamp 数据库乐观并发
- [x] Scheduler 数据库 Lease 获取 / Renewal / Release / Expiration Recovery
- [x] Node Agent 心跳超时自动标记 Offline
- [x] Node / WorkerSlot / NodePool 管理查询与节点状态控制
- [x] Node Drain / Disabled 控制
- [x] WorkerSlot 启停生命周期 API
- [x] ExecutionDispatch → NodeAgent SignalR ExecuteAsync
- [x] ExecutionProgress → Execution / TaskItem 状态持久化

### 待完成
- [ ] mTLS / 节点证书轮换
- [ ] 完整注册审批工作流（拒绝/撤销/重新申请）
- [ ] UKey / Hardware ResourceLock 原子抢占
- [ ] Dispatch 幂等键 / 故障安全重调度

## Phase 2：Workflow
- [x] Workflow CRUD / Version / Step Schema 基础 API
- [x] WorkflowVersion 发布数据模型
- [x] Task / TaskItem / Execution 基础模型
- [x] Locator / 子流程数据结构预留
- [x] Vue3 Workflow Designer 基础组件
- [x] WorkflowDefinitionValidator 发布前校验
- [x] Script Step 默认禁止发布
- [ ] Designer 接入真实 Workflow API
- [ ] 流程测试与单步调试
- [ ] 完整发布/停用策略
- [ ] BusinessSystem / WorkflowVersion / Task ExecutionRequirement 完整约束

## Phase 3：RPA Engine + NodeAgent
- [x] Playwright Browser Runtime
- [x] Navigate / Click / Input / Select / Wait / WaitForElement
- [x] Extract / Upload / Download / Screenshot / Assert
- [x] Condition / Loop / SubWorkflow 嵌套执行
- [x] 参数 `{{name}}` 解析
- [x] ExecutionCommand 通信协议与 NodeAgent 接收
- [x] ExecutionProgress Server 持久化入口
- [x] Workflow Script 默认安全拒绝
- [ ] Windows Desktop UI Worker
- [ ] Execution Log / Artifact 持久化
- [ ] 真正的 Cancel / Pause / Resume / HumanTask 恢复
- [ ] Step Timeout / Retry Policy
- [ ] UKey / Captcha / File Provider 接入 Runtime

## Phase 4：Scheduler 生产化
- [x] ExecutionRequirement / Capability Matching / NodePool
- [x] Required/Excluded Node / NetworkZone / Hardware ID
- [x] Worker Capacity / Node Health
- [x] 持久化 Worker Slot Lease 获取/释放
- [x] DB 乐观并发 Lease / Renewal / Expiration Recovery
- [ ] Node Affinity / Preferred Node / Credential Affinity
- [ ] UKey ResourceLock 原子抢占
- [ ] 调度幂等 / 故障安全重调度
- [ ] Permission-filtered candidate set
- [ ] 大规模节点调度性能优化

## Phase 5：Agent
- [ ] Chat / Intent Parser / Resource Resolver / TaskPlan
- [ ] Permission Pre-check / Confirmation
- [ ] Workflow Selection / ExecutionRequirement 解析
- [ ] 参数结构化 / 执行摘要
- [ ] LLM Provider / 本地模型 Provider

## Phase 6：批量业务
- [x] Task / TaskItem 基础模型
- [x] 每项独立状态 / RetryCount
- [x] CSV Import
- [x] XLSX 第一工作表 Import
- [x] 导入行自动生成独立 TaskItem
- [ ] 并发控制 / 跨 Node 并发
- [ ] 失败项重试 / 断点续跑策略增强
- [ ] 结果导出 / Artifact

## Phase 7：人工介入与集成
### 已完成
- [x] Hardware / Captcha / Notification Provider 抽象
- [x] UKey / Captcha / QR / Face Human Intervention 数据模型设计
- [x] HumanIntervention 持久化
- [x] HumanIntervention 创建 / 查询 / 完成 / 取消 API
- [x] 第三方 Captcha HTTP Adapter
- [x] Webhook Notification Provider（可配置钉钉/企业微信/网关）
- [x] SMTP Email Notification Provider
- [x] Windows Certificate Store UKey/智能卡发现 Provider
### 待实现
- [ ] 厂商 UKey SDK / PIN / 签名 Provider
- [ ] Captcha Provider 路由 / 超时 / 重试 / 熔断
- [ ] QR 临时安全入口 / 人工介入状态机
- [ ] 扫码后恢复 Browser Session / Execution
- [ ] 人脸实名认证接管
- [ ] 微信/企业微信原生 API 与 WebSocket 站内通知
- [ ] SMS / Webhook 完整策略

## Phase 8：运营中心
- [x] Node 状态管理 API
- [x] Node Drain / Disable
- [x] NodePool CRUD
- [x] WorkerSlot 启停管理
- [x] Human Intervention 管理 API
- [x] AuditEntry / 审计查询 API
- [x] API 变更审计中间件（不读取 Body，避免敏感信息入日志）
- [x] AgentRPA Vue3 控制中心基础 UI
- [x] Node Overview / Task Overview / Theme 切换组件
- [ ] Node / NodePool / Capability / WorkerSlot 完整管理 UI
- [ ] 调度监控 / ExecutionDispatch 时间线
- [ ] 人工介入 / UKey / 通知中心 UI
- [ ] 执行统计 / 失败分析 / 指标与资源监控

## Phase 9：扩展能力
- [ ] 更多城市 / 业务系统 Adapter
- [ ] 多 LLM Provider / 本地模型 / 云模型
- [ ] 对象存储 / Linux Node / Windows 云桌面
- [ ] 分布式 Worker / 自动扩缩容
- [ ] Provider / Plugin 注册机制

## 当前代码完成度

当前已经从架构骨架进入**节点安全接入 + 持久化调度 + 浏览器 Runtime + 批量导入 + 集成 Provider + 运营中心基础 UI**阶段。

- Domain：Node / Pool / Capability / WorkerSlot + AgentKey + NodeLease / ResourceLock + Task / Execution + HumanIntervention + AuditEntry 已具备。
- Application：ExecutionRequirement、Scheduler、Node Registry、Spreadsheet Import 已具备。
- Contracts：注册、心跳、Heartbeat ACK、ExecutionCommand、ExecutionProgress、HumanIntervention / Integration 契约已具备。
- Infrastructure：EF Core 10 + SQLite、Node Registry、乐观并发 Worker Lease、Captcha / Notification / Windows Certificate Provider、审计持久化已落地。
- API：Node 注册认证/审批/状态、SignalR Hub、ExecutionDispatch、ExecutionProgress、Task CSV/XLSX Import、HumanIntervention、Audit、ProblemDetails 已接入。
- NodeAgent：已具备注册、Bootstrap Key、审批等待、SignalR 自动重连、命令接收与 Playwright Runtime。
- Frontend：Vue3 + TypeScript + Vite，已有控制中心、公共状态组件、Node/Task 概览、Workflow Designer 基础组件，并有独立 GitHub Actions Build。
- 尚未生产化：JWT/Identity、完整权限引擎、厂商 UKey SDK、QR/Face 恢复、Desktop UI、Agent、完整通知中心、自动化测试、完整运营 UI、扩展插件机制。

## 开发规则

1. 完成子阶段立即同步 `[x]/[ ]`。
2. 架构变化同步 README / architecture / data-model / api-design / 本文档。
3. “已完成”必须有代码、文档或测试证据。
4. 保持每阶段可编译、可运行、可回滚。
5. 前端优先复用公共组件，禁止复制 CRUD 逻辑。
6. API、领域、Application 及复杂前端逻辑提供中文注释/说明。
7. **每次代码或文档推送后必须检查 GitHub Actions；Build 失败立即定位、修复并再次轮巡，直到最新提交 Build 成功后再进入下一项任务。**
8. **只要本文档存在未完成任务，就继续推进，不以单个阶段完成作为最终停止条件。**

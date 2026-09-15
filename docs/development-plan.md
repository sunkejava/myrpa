# 开发实施计划

## Phase 0：基础工程

- .NET 10 Solution
- DDD/Clean Architecture
- Vue 3 + TypeScript
- 统一 API Client
- 统一异常处理
- JWT/Identity
- 数据库迁移
- i18n
- Theme Design Token
- 基础 Layout
- 通用 SearchForm/DataTable/FormDialog

## Phase 1：平台基础

- 用户
- 角色
- 组织
- 城市
- 业务系统
- 业务功能
- 细粒度权限
- 审计
- ExecutionNode
- NodeCapability
- NodePool
- WorkerSlot
- Node 注册/心跳/状态

验收标准：

1. 可以完成“北京 → 社保系统 → 社保缴费查询 → 执行”粒度的权限授权。
2. 一台服务器可以同时管理多台执行客户端。
3. 服务端可以看到节点 OS、NodeKind、能力、网络区域、Worker 状态。

## Phase 2：Workflow

- Workflow CRUD
- Workflow Version
- Step Schema
- 子流程
- Locator 管理
- 流程测试
- 发布/停用
- ExecutionRequirement
- BusinessSystem 默认执行环境要求
- WorkflowVersion 执行环境要求

验收标准：可以配置“该系统必须 Windows”“该 Workflow 必须 Windows + DesktopUI”等规则。

## Phase 3：RPA Engine

- Browser Worker
- 浏览器生命周期
- Step Runner
- Retry Policy
- Screenshot
- Download/Upload
- Execution Log
- 人工接管
- Windows Node Agent
- Node 与 Server 通信
- WorkerSlot
- NodeLease
- ExecutionDispatch

## Phase 4：Execution Scheduler

- Task Queue
- ExecutionRequirement Resolver
- Node Capability Matching
- NodePool
- Node Affinity
- Required/Preferred/Excluded Node
- Network Zone
- UKey Resource Lock
- Credential Affinity
- Worker Capacity
- Node Health
- 任务跨节点派发
- 节点故障后的安全重调度

验收标准：

- Windows 要求的任务不会派发到 Linux。
- UKey 任务只派发到拥有指定 UKey 的节点。
- 100 条 TaskItem 可以分配到多台 Node 并行执行。
- Node 离线后不再接收新任务。
- 对不可逆业务操作不能无条件自动重试。

## Phase 5：Agent

- Chat
- Intent Parser
- Resource Resolver
- TaskPlan
- Permission Check
- Confirmation
- Workflow Selection
- ExecutionRequirement 解析

## Phase 6：批量业务

- Excel/CSV
- TaskItem
- 并发控制
- 跨 Node 并发
- 失败重试
- 断点续跑
- 结果导出

## Phase 7：人工介入与集成能力

- Captcha Provider
- QRCode Human Intervention
- Face/RealName Human Intervention
- UKey Provider
- Notification Center
- Email
- 钉钉
- 微信
- 企业微信
- WebSocket/站内通知
- SMS/Webhook 扩展

## Phase 8：运营中心

- 机器人资源池
- Node 状态
- NodePool 管理
- 节点能力管理
- 实时任务监控
- 实时执行节点地图/拓扑
- 执行统计
- 失败分析
- 审计查询
- 节点资源监控

## Phase 9：扩展能力

- 更多城市
- 更多业务系统 Adapter
- 多 LLM Provider
- 本地模型
- 云模型
- 对象存储
- Linux Node
- Windows 云桌面
- 分布式 Worker
- 自动扩缩容

## 开发质量要求

每个 Phase 必须保持可运行状态。业务页面优先复用公共组件；发现重复实现时先抽取组件再继续开发。所有 API、实体、服务和复杂前端逻辑都需要中文注释或文档说明，方便后续维护。

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
| Phase 5 | Agent | 🟡 已完成资源解析、动作/风险识别、已发布 Workflow 选择；权限确认、参数结构化、LLM Provider 待完成 |
| Phase 6 | 批量业务 | 🟢 CSV/XLSX 导入、TaskItem 独立状态/重试已完成；跨 Node 并发、断点续跑、结果 Artifact 待完成 |
| Phase 7 | 人工介入与外部集成 | 🟢 HumanIntervention 生命周期、Captcha HTTP Adapter、Webhook/Email、Windows 证书型 UKey Provider 已具备；QR 恢复链路与厂商 UKey SDK 待完成 |
| Phase 8 | 运营中心 | 🟢 Node/Pool/Worker 管理 API、人工介入 API、审计模型/API、控制中心前端骨架已完成；完整实时运营 UI/指标待完成 |
| Phase 9 | 扩展能力 | ⚪ 未开始 |

## Phase 5：Agent
- [x] Chat / Intent Parser / Resource Resolver / TaskPlan 基础规划器
- [x] 根据城市 → 系统 → 功能解析资源目录
- [x] 动作与风险等级基础识别
- [x] 仅允许绑定已发布 WorkflowVersion，并拒绝多 Workflow 歧义
- [ ] Permission Pre-check / Confirmation
- [ ] Workflow ExecutionRequirement 解析并合并到调度要求
- [ ] 参数结构化 / 执行摘要
- [ ] LLM Provider / 本地模型 Provider

## 其他阶段剩余任务

### Phase 0
- [ ] NuGet 集中版本管理
- [ ] JWT / Identity
- [ ] EF Core Migration
- [ ] 完整 API Client / i18n / Theme Settings
- [ ] 自动化测试与 CI test/publish

### Phase 1
- [ ] mTLS / 节点证书轮换
- [ ] 完整注册审批工作流（拒绝/撤销/重新申请）
- [ ] UKey / Hardware ResourceLock 原子抢占
- [ ] Dispatch 幂等键 / 故障安全重调度

### Phase 2
- [ ] Designer 接入真实 Workflow API
- [ ] 流程测试与单步调试
- [ ] 完整发布/停用策略
- [ ] BusinessSystem / WorkflowVersion / Task ExecutionRequirement 完整约束

### Phase 3
- [ ] Windows Desktop UI Worker
- [ ] Execution Log / Artifact 持久化
- [ ] 真正的 Cancel / Pause / Resume / HumanTask 恢复
- [ ] Step Timeout / Retry Policy
- [ ] UKey / Captcha / File Provider 接入 Runtime

### Phase 4
- [ ] Node Affinity / Preferred Node / Credential Affinity
- [ ] UKey ResourceLock 原子抢占
- [ ] 调度幂等 / 故障安全重调度
- [ ] Permission-filtered candidate set
- [ ] 大规模节点调度性能优化

### Phase 6
- [ ] 并发控制 / 跨 Node 并发
- [ ] 失败项重试 / 断点续跑策略增强
- [ ] 结果导出 / Artifact

### Phase 7
- [ ] 厂商 UKey SDK / PIN / 签名 Provider
- [ ] Captcha Provider 路由 / 超时 / 重试 / 熔断
- [ ] QR 临时安全入口 / 人工介入状态机
- [ ] 扫码后恢复 Browser Session / Execution
- [ ] 人脸实名认证接管
- [ ] 微信/企业微信原生 API 与 WebSocket 站内通知
- [ ] SMS / Webhook 完整策略

### Phase 8
- [ ] Node / NodePool / Capability / WorkerSlot 完整管理 UI
- [ ] 调度监控 / ExecutionDispatch 时间线
- [ ] 人工介入 / UKey / 通知中心 UI
- [ ] 执行统计 / 失败分析 / 指标与资源监控

### Phase 9
- [ ] 更多城市 / 业务系统 Adapter
- [ ] 多 LLM Provider / 本地模型 / 云模型
- [ ] 对象存储 / Linux Node / Windows 云桌面
- [ ] 分布式 Worker / 自动扩缩容
- [ ] Provider / Plugin 注册机制

## 开发规则

1. 完成子阶段立即同步 `[x]/[ ]`。
2. 架构变化同步 README / architecture / data-model / api-design / 本文档。
3. “已完成”必须有代码、文档或测试证据。
4. 保持每阶段可编译、可运行、可回滚。
5. 前端优先复用公共组件，禁止复制 CRUD 逻辑。
6. API、领域、Application 及复杂前端逻辑提供中文注释/说明。
7. **每次代码或文档推送后必须检查 GitHub Actions；Build 失败立即定位、修复并再次轮巡，直到最新提交 Build 成功后再进入下一项任务。**
8. **只要本文档存在未完成任务，就继续推进，不以单个阶段完成作为最终停止条件。**

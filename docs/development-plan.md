# AgentRPA 开发实施计划与实时进度

> 架构发生变化时先同步本文档；本文件作为阶段进度基准。

## 总体进度

| 阶段 | 主题 | 状态 |
|---|---|---|
| Phase 0 | 基础工程与统一构建 | 🟢 基础工程、JWT、数据库用户/角色/登录、PBKDF2 密码存储已落地；Migration、测试等待建设 |
| Phase 1 | 平台基础 + 一托 N 执行节点 | 🟢 注册认证、审批状态、节点健康、数据库乐观并发 Lease、节点池/WorkerSlot 管理已落地；mTLS/硬件锁/故障重调度待完成 |
| Phase 2 | Workflow | 🟢 Workflow / Version / Step / Task API 与前端 Designer 已具备；在线调试与完整发布策略待完成 |
| Phase 3 | RPA Engine / NodeAgent | 🟢 Playwright 确定性 Step Runner 已支持浏览器常用步骤；Desktop、HumanTask 恢复与完整执行控制待完成 |
| Phase 4 | Scheduler 生产化 | 🟢 Capability Matching、NodePool、Worker Lease、数据库乐观并发抢占、业务权限复核已具备；ResourceLock/故障重调度待完成 |
| Phase 5 | Agent | 🟡 已完成资源解析、动作/风险识别、Workflow 选择、参数基础结构化、权限预检查、确认门禁、OpenAI Compatible/llama.cpp 结构化解析兜底；完整参数 Schema/执行摘要待完成 |
| Phase 6 | 批量业务 | 🟢 CSV/XLSX 导入、TaskItem 独立状态/重试已完成；跨 Node 并发、断点续跑、结果 Artifact 待完成 |
| Phase 7 | 人工介入与外部集成 | 🟢 HumanIntervention 生命周期、Captcha HTTP Adapter、Webhook/Email、Windows 证书型 UKey Provider 已具备；QR 恢复链路与厂商 UKey SDK 待完成 |
| Phase 8 | 运营中心 | 🟢 Node/Pool/Worker 管理 API、人工介入 API、审计模型/API、控制中心前端骨架已完成；完整实时运营 UI/指标待完成 |
| Phase 9 | 扩展能力 | ⚪ 未开始 |

## Phase 0：身份认证
- [x] JWT Bearer：Issuer/Audience/SigningKey/Lifetime 校验
- [x] 数据库 UserAccount / Role / UserRole 持久化
- [x] PBKDF2-SHA256 密码哈希与恒时验证
- [x] 登录 `/api/auth/login` 与当前用户 `/api/auth/me`
- [x] JWT Role Claim 与 Admin API 对接
- [x] 首次启动按显式配置创建 Admin，不配置密码则不生成默认账户
- [ ] EF Core Migration
- [ ] 自动化测试与 CI test/publish

## Phase 4：权限与调度
- [x] 用户直授权限：城市 → 系统 → 功能 → Action 精确匹配
- [x] 角色继承权限：User → Role → RoleAccessPolicy
- [x] Agent/Task 在执行前统一权限校验
- [x] Scheduler 阶段保留执行主体并可再次复核业务权限
- [ ] UKey ResourceLock 原子抢占
- [ ] Dispatch 幂等键 / 故障安全重调度
- [ ] 大规模节点调度性能优化

## 其他阶段剩余任务

### Phase 5
- [ ] 完整结构化参数 Schema / 执行摘要
- [ ] 持久化 LLM Token 统计

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

# AgentRPA 开发实施计划与实时进度

> 架构发生变化时先同步本文档；本文件作为阶段进度基准。

## 总体进度

> 2026-09-25 复核：表中的绿色标记仅代表原仓库该阶段已有部分基础能力，并非整阶段验收通过。下方未完成项与新增的验收缺口必须继续完成。

## 本轮已验证的纵向能力

- [x] 原项目 .NET 10 Solution 在 GitHub Actions Release 构建通过；现有后端测试及 Vue 构建通过。
- [x] API 在全新 SQLite 数据库执行 Migration 后启动，管理员登录、匿名接口拦截通过 CI 冒烟测试。
- [x] 城市、业务系统、业务功能可由管理员创建；停用的城市/系统不进入 Agent 资源目录及权限检查。
- [x] 普通用户创建、城市资源精确授权，以及跨城市/错误系统 ID 拒绝有可执行 CI 冒烟脚本。
- [x] Workflow Step 的 `requiredAction` 可逐步预检；任务创建、入队与派发都会复核，包含嵌套分支。
- [x] 同一精确资源范围支持显式 Deny，优先于用户/角色 Allow；API 和 UI 支持直接拒绝与恢复授权。
- [x] 管理员可管理角色的精确资源 Allow/Deny/撤销；CI 冒烟覆盖角色授权与用户拒绝的优先级。
- [x] 权限中心前端可为用户或角色设置精确 Allow/Deny，并显示及撤销双方的策略。
- [x] 高风险任务服务端待审批记录、管理员审批中心及任务所有者获批后入队入口；审批不可由发起人本人完成。
- [x] Workflow 管理页接入真实资源选择、定义编辑、创建版本与发布；风险标记可通过页面设置，停用后阻止新任务和未派发任务。
- [x] 自动与手动失败重试只允许经完整遍历确认的只读 Workflow；写入、下载、人工与高风险流程禁止盲目重试，Step 开始/结束写入执行日志供核验。
- [x] 任务中心可按所有者查看任务项、执行实例、Step 检查点和脱敏日志，帮助人工核验外部提交状态。
- [x] Country、Province、City、District 基础实体及父子资源 API；历史 City 的 ProvinceId 保持可空，迁移不阻断现有数据。
- [x] 国家/省份停用后，子级资源创建、角色授权与现有任务权限复核均拒绝；CI 覆盖两级父资源停用。
- [x] 前端接入真实登录、自然语言任务规划、任务列表和城市/系统/功能管理。
- [x] Chromium 浏览器端验证桌面登录 → Planner → 任务提交与手机视口主题/布局（首轮发现并修复 SQLite APPLY 查询错误）。

## 核心验收缺口

- [ ] Country → Province → City → District 树的租户边界及跨层级权限范围（基础实体/API、父级停用权限检查和前端管理已落地）。
- [ ] Capability 与 Agent SDK/注册路由；逐 Step Capability/Tool 权限以及跨层级资源继承。当前仅支持 Step 所声明的业务动作和精确范围 Deny。
- [ ] 高风险操作服务端审批门禁，含任务、步骤和批量审批（任务级持久化审批、不同管理员复核、入队与派发门禁已实现；逐 Step 与批量审批仍待完成）。
- [x] 青岛 Mock 社保增减员全链路及可替换 Playwright Adapter（开发环境隔离的模拟站点、登录、身份证校验、增减员冲突处理、浏览器测试及增减员 Workflow 的真实 NodeAgent 审批、派发、执行与检查点集成验收；站点定位符可通过 `IWorkflowSiteAdapter` 替换，节点能力过滤避免错误派发）。
- [ ] 高风险提交幂等、外部状态核验、Checkpoint 恢复与自动恢复测试（Step 事件、盲重试阻断与人工核验后结案/重试已具备；Mock 增减员回执与参保记录已持久化，CI 验证 API 重启后同号重放；管理员可只读核验失败执行对应 Mock 回执与当前参保状态，缺失或不匹配时保持不确定，核验结论仍由管理员确认；真实外部系统的幂等、自动对账和安全断点续跑尚待完成）。
- [ ] 真实浏览器端测试、手机布局验证、100 任务/1000 子任务压力测试。
- [ ] Docker/PostgreSQL/Redis/MAF、可观测性与生产部署验收。

| 阶段 | 主题 | 状态 |
|---|---|---|
| Phase 0 | 基础工程与统一构建 | 🟢 JWT、数据库用户/角色/登录、PBKDF2、EF Design-time、InitialCreate、自动 Migration、Migration-first 初始化、CI Build/Test 已落地；发布流水线仍待完善 |
| Phase 1 | 平台基础 + 一托 N 执行节点 | 🟢 注册认证、审批状态、节点健康、数据库乐观并发 Lease、节点池/WorkerSlot 管理已落地；mTLS 待完成 |
| Phase 2 | Workflow | 🟢 Workflow / Version / Step / Task API 与前端 Designer 已具备；在线调试与完整发布策略待完成 |
| Phase 3 | RPA Engine / NodeAgent | 🟢 Playwright 确定性 Step Runner、HumanTask 等待/恢复原浏览器会话已支持；运行时截图/下载/上传产物登记已支持；Desktop、完整执行控制仍待完成 |
| Phase 4 | Scheduler 生产化 | 🟢 Capability Matching、NodePool、Worker Lease、业务权限复核、UKey/硬件 ResourceLock 原子抢占、派发幂等、多实例竞争恢复、无资源重调度、PreferredNode/CredentialAffinity 软评分已具备；大规模性能优化待完成 |
| Phase 5 | Agent | 🟡 已完成资源解析、动作/风险识别、Workflow 选择、参数基础结构化、权限预检查、确认门禁、OpenAI Compatible/llama.cpp 结构化解析兜底、LLM Token 用量持久化与用户隔离查询；完整参数 Schema/执行摘要待完成 |
| Phase 6 | 批量业务 | 🟢 CSV/XLSX 导入、TaskItem 独立状态/重试、ExecutionLog/Artifact 元数据及查询、NodeAgent 运行时 Artifact 自动登记已完成；Artifact 实体存储/下载仍待完成 |
| Phase 7 | 人工介入与外部集成 | 🟢 HumanIntervention 生命周期、Captcha HTTP Adapter、Webhook/Email、Windows 证书型 UKey Provider、QR 短期一次性令牌及人工完成后恢复 Execution/Browser Session 已具备；厂商 UKey SDK 待完成 |
| Phase 8 | 运营中心 | 🟢 Node/Pool/Worker 管理 API、人工介入 API、审计模型/API、执行日志/产物查询 API、控制中心前端骨架已完成；完整实时运营 UI/指标待完成 |
| Phase 9 | 扩展能力 | ⚪ 未开始 |

## Phase 5：Agent
- [x] 资源解析、动作/风险识别、Workflow 选择与参数基础结构化
- [x] 权限预检查与高风险确认门禁
- [x] OpenAI Compatible / llama.cpp 结构化解析兜底
- [x] LLM Token 用量持久化：主体、Provider、Model、Input/Output/Total Tokens
- [x] LLM Token 用量用户隔离查询与汇总 API，SQLite 使用 ID 稳定排序
- [ ] 完整结构化参数 Schema / 字段类型、必填项与业务校验
- [ ] 执行摘要 / 可审阅的自然语言计划说明

## Phase 6：执行证据
- [x] ExecutionLog：执行级/Step 级日志、Sequence 稳定排序、敏感标记
- [x] ExecutionArtifact：文件名、StorageKey、Hash、大小、类型、过期时间等元数据
- [x] 当前用户执行日志/产物元数据隔离查询 API
- [x] NodeAgent Runtime 自动上报运行时 Artifact 元数据
- [x] Screenshot / Download / Upload 等运行时 Artifact 自动登记
- [ ] Artifact Storage Provider（本地文件、对象存储）

## 其他阶段剩余任务

### Phase 0
- [x] Release/Publish 自动化：Linux/Windows 自包含 API、NodeAgent 与前端静态产物打包、Linux 发布产物启动冒烟、版本标签生成 GitHub Release；部署与浏览器依赖说明见 `docs/deployment.md`。

### Phase 1
- [ ] mTLS / 证书轮换
- [ ] 节点拒绝 / 吊销 / 重新申请审批流程（已支持拒绝后相同 AgentKey 再申请、管理员重新审批；空闲节点吊销后原身份禁止注册和心跳，须更换 AgentKey；吊销操作拦截未结束执行，管理 UI 与审计留痕待补）。
- [ ] 故障重调度完整性测试（节点离线且租约过期的运行中 Execution 已转入失败核验、释放资源且不自动盲重试；真实节点强制退出的浏览器链路已覆盖，高可用多实例与恢复策略仍待验收）。

### Phase 2
- [ ] Designer 完整 API（创建、版本查询、发布和停用已接入页面；逐 Step 表单与在线调试仍待完成）
- [ ] Workflow 在线测试 / Step Debug
- [ ] 完整 Publish / Disable 策略（停用后阻断新任务及待派发任务，NodeAgent 并发处理执行与取消/恢复命令，节点重连时补发取消并阻止人工恢复，StepStarted 再次检查停用状态；取消记录失败供高风险任务核验，CI 覆盖运行中 Wait 和人工等待阶段停用且未进入外部提交步骤。节点进程退出后丢失会话与正在进行的外部副作用仍不能保证立即中止，待完成生产级安全停机）。
- [ ] ExecutionRequirement 全约束覆盖

### Phase 3
- [ ] Windows Desktop UI Worker
- [x] HumanTask / Resume：NodeAgent 保持原 Playwright Browser Session 等待人工完成；服务端通过 SignalR ResumeAsync 恢复
- [ ] Cancel / Pause 完整运行时控制（任务所有者可取消待派发或运行中的任务；取消命令通知在线节点、重连补发，运行中断后保留失败执行供人工核验；暂停与恢复、并发取消竞态的完整验收仍待完成）。
- [x] Step Timeout / Retry：按 Step 指定 timeoutMs；仅 Navigate、WaitForElement、Assert、Extract 允许最多 3 次重试，发布校验包括嵌套 Step；Click/Upload/Download 等有副作用步骤禁止自动重试。
- [ ] UKey / Captcha / File Provider Runtime 集成

### Phase 4
- [x] PreferredNodeIds：硬过滤后优先选择业务指定节点
- [x] CredentialAffinityKey：硬过滤后优先选择声明匹配凭据亲和能力的节点，不替代 Credential 授权
- [ ] 大规模节点调度性能优化

### Phase 7
- [ ] 厂商 UKey SDK / PIN / 签名 Provider
- [ ] Captcha Provider 路由 / 超时 / 重试 / 熔断
- [x] QR 临时安全入口：短期随机令牌、SHA-256 摘要存储、主体/Execution 绑定、一次性条件消费、过期状态
- [x] 扫码/人工完成后恢复 Browser Session / Execution；NodeAgent 短暂断线后重连会补发 Resume
- [ ] 人脸实名认证接管
- [ ] 微信/企业微信原生 API 与 WebSocket 站内通知
- [ ] SMS / Webhook 完整策略

### Phase 8
- [ ] Node / NodePool / Capability / WorkerSlot 完整管理 UI（节点管理页支持审核、排空、吊销和 WorkerSlot 启停；节点池页支持创建、重命名、停启、删除和分配；能力页支持禁用/启用已上报能力，Agent 重注册不会取消管理员禁用；节点详情已展示能力、槽位、执行状态统计及最近执行；CPU/内存等运行指标采集与展示待完成）。
- [x] 调度监控 / ExecutionDispatch 时间线（已记录自动及人工派发、离线回退，归属隔离的执行事件与租约时间线；管理员调度页汇总任务、执行、节点与槽位状态及最近执行）
- [ ] 人工介入 / UKey / 通知中心 UI（人工介入独立页面已支持创建、列表、完成、取消和一次性 QR 令牌消费；UKey 设备与通知中心待完成）。
- [ ] 执行统计 / 失败分析 / 指标与资源监控（任务详情已查看日志、检查点和产物下载；独立审计查询与个人 LLM Token 用量页面已接入；账户和角色管理页可创建、分配及停启账户；聚合指标与失败分析待完成）。

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

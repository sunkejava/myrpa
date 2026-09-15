# AgentRPA

> 面向业务人员的自然语言驱动 RPA 自动化平台。

AgentRPA 的目标不是简单提供一个“录制网页操作”的 RPA 工具，而是建立一套 **自然语言 → 业务意图 → 权限校验 → 任务规划 → RPA 流程 → 浏览器执行 → 数据处理 → 结果审计** 的自动化平台。

用户只需要描述业务目标，例如：

> 登录北京市社保系统，根据人员 Excel 查询指定月份的社保缴费情况，并将结果导出为 Excel。

系统负责识别城市、业务系统、具体业务功能、输入数据和输出目标，在确认用户拥有对应权限后选择合适的 RPA 流程并执行。

---

## 1. 项目目标

### 1.1 核心目标

- 支持自然语言创建自动化任务。
- 支持不同城市、不同业务系统、不同业务功能的独立配置。
- 权限颗粒度达到 **城市 → 系统 → 功能 → 操作**。
- Agent 负责理解和规划，RPA Engine 负责确定性执行。
- 支持 Excel、CSV、TXT、PDF、Word 等业务文件作为输入或输出。
- 支持验证码、短信、UKey、扫码等场景下的人工接管。
- 支持任务暂停、恢复、取消、重试和失败续跑。
- 对所有敏感业务操作提供完整审计记录。
- 业务网站发生变化时，可以通过更新 Workflow 版本适配，而无需修改 Agent 核心逻辑。

### 1.2 设计原则

1. **Agent 与 RPA 解耦**：大模型负责理解和规划，不直接替代确定性自动化流程。
2. **权限前置**：任务执行前必须完成城市、系统、功能和操作权限校验。
3. **配置驱动**：城市、系统、功能、流程、参数和凭据尽可能配置化。
4. **可恢复执行**：长任务必须支持断点、重试和失败项续跑。
5. **人工可接管**：无法自动处理的认证和交互必须能够安全暂停并恢复。
6. **全程可审计**：记录任务、流程、用户、系统、结果和异常，但敏感数据必须脱敏。
7. **多租户/组织可扩展**：后续支持单位、部门、人员等组织隔离。

---

## 2. 总体业务链路

```text
自然语言请求
      │
      ▼
Agent 意图识别
      │
      ▼
城市 / 系统 / 功能 / 参数识别
      │
      ▼
权限校验
      │
      ├── 无权限 → 拒绝执行
      │
      ▼
TaskPlan 任务规划
      │
      ▼
选择 Workflow + Workflow Version
      │
      ▼
准备输入数据 / 凭据
      │
      ▼
RPA Engine
      │
      ▼
Browser / Desktop / File Automation
      │
      ├── 需要人工 → WaitingHuman
      │                 │
      │                 ▼
      │              人工完成
      │                 │
      │                 ▼
      │               恢复
      │
      ▼
结果采集与校验
      │
      ▼
输出文件 / 任务报告
      │
      ▼
审计日志
```

---

## 3. 核心领域模型

```text
User
 └── Role
      └── AccessPolicy
           ├── City
           ├── System
           ├── Function
           └── Action

City
 └── BusinessSystem
      └── BusinessFunction
           └── Workflow
                └── WorkflowVersion

User
 └── Task
      ├── TaskPlan
      ├── TaskExecution
      ├── TaskData
      ├── TaskResult
      └── AuditLog
```

### 3.1 权限模型

最终权限示例：

```text
张三
├── 北京
│   └── 北京社保系统
│       ├── 人员查询       查看 / 执行
│       ├── 缴费查询       查看 / 执行 / 导出
│       └── 申报           无权限
└── 上海
    └── 上海社保系统
        └── 人员查询       查看
```

权限检查必须在 Agent 规划完成后、RPA 执行前完成。

---

## 4. 核心功能模块

| 模块 | 主要职责 |
|---|---|
| Agent 对话 | 接收自然语言任务 |
| Agent Planner | 识别意图、实体、参数并生成 TaskPlan |
| 城市管理 | 管理业务地区 |
| 业务系统管理 | 管理不同城市的业务网站/系统 |
| 功能管理 | 管理具体业务功能 |
| 权限管理 | 城市/系统/功能/操作级授权 |
| Workflow 管理 | 管理 RPA 流程及版本 |
| RPA Engine | 执行确定性自动化操作 |
| Browser Engine | 浏览器控制、页面、元素、下载上传等 |
| Credential | 管理账号、密码、证书等敏感凭据 |
| Task Center | 任务队列、状态、重试、暂停、恢复 |
| Human Intervention | 人工接管与恢复 |
| Data Engine | Excel/CSV/PDF/Word 等数据处理 |
| Audit | 审计与执行日志 |
| Monitor | 运行状态、成功率、耗时、异常监控 |
```

---

## 5. 典型业务场景

### 5.1 社保人员缴费查询

用户：

> 查询人员信息.xlsx 中所有人员 2026 年 8 月的社保缴费情况。

系统解析：

```json
{
  "city": "北京",
  "system": "社保系统",
  "function": "社保缴费查询",
  "inputFile": "人员信息.xlsx",
  "period": "2026-08",
  "action": "query",
  "output": "Excel"
}
```

执行：

1. 读取 Excel。
2. 校验人员字段。
3. 检查用户权限。
4. 创建任务计划。
5. 登录对应城市业务系统。
6. 打开缴费查询功能。
7. 按人员逐项查询。
8. 采集并校验结果。
9. 单条失败自动重试。
10. 生成结果 Excel。
11. 生成任务报告和审计记录。

---

## 6. 任务状态

```text
Pending
  ↓
Planning
  ↓
PermissionChecking
  ↓
Queued
  ↓
Running
  ├── Retrying
  ├── WaitingHuman
  ├── WaitingLogin
  └── WaitingCaptcha
  ↓
Completed

异常终态：
Failed
Cancelled
Timeout
PermissionDenied
```

---

## 7. 安全要求

涉及社保、医保、公积金等业务时，必须按照敏感业务系统标准设计。

- 密码、Token、证书等凭据必须加密保存。
- 前端不得返回完整敏感凭据。
- 日志不得记录密码、验证码等机密信息。
- 身份证号、手机号等敏感字段默认脱敏。
- 文件访问必须经过用户/组织权限校验。
- Task 执行必须绑定发起用户和授权上下文。
- Agent 不得通过自然语言绕过权限系统。
- Workflow 执行时再次校验任务授权上下文，避免越权执行。

---

## 8. 文档目录

- [`docs/requirements.md`](docs/requirements.md)：完整产品需求规格说明。
- [`docs/architecture.md`](docs/architecture.md)：总体技术架构与模块边界。
- [`docs/permission-model.md`](docs/permission-model.md)：城市/系统/功能/操作权限模型。
- [`docs/agent-task-flow.md`](docs/agent-task-flow.md)：Agent 任务理解、规划与执行链路。
- [`docs/rpa-workflow.md`](docs/rpa-workflow.md)：RPA Workflow 与版本管理设计。
- [`docs/security.md`](docs/security.md)：凭据、敏感数据、审计和安全要求。

---

## 9. 开发阶段

### Phase 1：基础平台

- 用户、角色、组织
- 城市管理
- 业务系统管理
- 功能管理
- 权限管理
- 基础任务中心

### Phase 2：RPA Engine

- 浏览器自动化
- Workflow 执行器
- 页面元素定位
- 文件上传/下载
- 截图和执行日志
- 重试与断点恢复

### Phase 3：Agent

- 自然语言理解
- 城市/系统/功能识别
- 参数提取
- TaskPlan
- 权限预检查
- Workflow 匹配

### Phase 4：企业级能力

- 人工接管
- 调度中心
- 凭据中心
- 运行监控
- 审计中心
- 多组织隔离
- Workflow 市场/模板

---

## 10. 当前原则

AgentRPA 的核心不是“让 AI 随便点击网页”，而是：

> **让 AI 理解业务，让权限控制边界，让 Workflow 保证确定性，让 RPA Engine 执行，让审计记录全过程。**

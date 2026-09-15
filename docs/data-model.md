# 核心数据模型

## 1. 基础组织

- User：用户
- Role：角色
- UserRole：用户角色关系
- Organization：组织

## 2. 业务资源

- City：城市
- BusinessSystem：城市下的业务系统
- BusinessFunction：系统下的具体功能
- FunctionAction：功能允许的操作

关系：`City 1:N BusinessSystem 1:N BusinessFunction 1:N FunctionAction`

## 3. 权限

建议使用 AccessPolicy 表达授权范围：

`SubjectType + SubjectId + CityId + SystemId + FunctionId + Action`

其中 SystemId、FunctionId 可为空以表达城市级或系统级授权，但执行具体功能时必须解析到完整资源链并进行最终校验。

## 4. Agent

- AgentSession：对话会话
- AgentMessage：用户输入和 Agent 输出
- TaskPlan：结构化任务规划
- TaskPlanVersion：规划版本

## 5. RPA

- Workflow
- WorkflowVersion
- WorkflowStep
- WorkflowVariable
- WorkflowCredentialRef
- ExecutionRequirement：执行环境要求

## 6. 执行节点

一托 N 架构的核心模型：

- ExecutionNode：一台可被平台调度的执行节点
- NodePool：执行节点池，用于按组织/业务/网络区域进行资源隔离
- NodeCapability：节点能力
- NodeRuntime：节点运行时信息
- NodeLease：节点/Worker 租约
- WorkerSlot：节点上的实际执行槽位
- NodeCredential：节点安全身份引用
- ResourceLock：UKey、桌面会话等独占资源锁

### ExecutionNode 建议字段

```text
Id
Name
NodeKind
OsPlatform
Architecture
Status
NodePoolId
NetworkZone
AgentVersion
LastHeartbeatAt
Enabled
```

`NodeKind` 与 `OsPlatform` 必须分开：

```text
Windows 物理机：Physical + Windows
Windows 虚拟机：VirtualMachine + Windows
Windows 云桌面：CloudDesktop + Windows
Linux 容器：Container + Linux
```

这样不会把“虚拟机”错误地当成操作系统条件。

### NodeCapability

用于描述节点“能做什么”，例如：

```text
Browser.Chrome
Browser.Edge
DesktopUI
Office.Excel
UKey
CertificateStore
Camera
FileUpload
FileDownload
PowerShell
Network.Government
```

能力建议带版本、状态和元数据，便于 Scheduler 判断兼容性。

## 7. 执行环境要求

BusinessSystem、WorkflowVersion、Task 均可以声明 `ExecutionRequirement`。

典型条件：

```text
OsPlatform
NodeKinds
Architecture
Browser
BrowserVersion
RequiredCapabilities
ForbiddenCapabilities
NetworkZone
RequiredNodePool
PreferredNodeIds
RequiredNodeIds
ExcludedNodeIds
ConcurrencyLimit
```

推荐约束关系：

```text
BusinessSystem Requirement
        ↓
WorkflowVersion Requirement
        ↓
Task Requirement
```

下层只能增加限制或覆盖允许范围，不能通过 Task 降低上层安全约束。

例如：

```text
BusinessSystem：Windows
Workflow：Windows + DesktopUI
Task：Windows + DesktopUI + UKey
```

最终调度条件为三者的安全交集。

## 8. 调度

- Task：一次用户任务
- TaskItem：批量任务中的单条业务数据
- Execution：一次实际 Workflow 执行
- ExecutionDispatch：一次任务向执行节点的派发记录
- ExecutionNode：实际执行节点
- WorkerSlot：实际执行槽位
- RetryRecord：重试记录

关系：

```text
Task
 ├── TaskItem
 │     └── Execution
 │           └── ExecutionDispatch
 │                 ├── ExecutionNode
 │                 └── WorkerSlot
 └── ...
```

一个 Task 可以包含多个 TaskItem，不同 TaskItem 可以同时运行在不同 Node 上。

## 9. 执行节点调度规则

Scheduler 首先进行硬条件过滤：

```text
OS
↓
NodeKind
↓
Architecture
↓
RequiredCapabilities
↓
NetworkZone
↓
NodePool
↓
RequiredNodeIds / ExcludedNodeIds
↓
Credential / UKey Affinity
↓
Health
↓
Capacity
```

硬条件不满足的 Node 不允许执行，即使该节点当前空闲。

通过硬条件后，再进行软评分：

```text
PreferredNode
BusinessSystem Affinity
Worker Load
Latency
Priority
```

## 10. 批量执行示例

Excel 100 条数据可以拆成：

```text
Task-001
 ├── Item-001 → Node-WinVM-01
 ├── Item-002 → Node-WinVM-01
 ├── ...
 ├── Item-031 → Node-WinVM-02
 ├── ...
 └── Item-100 → Node-WinPC-01
```

如果 Workflow 要求 `OsPlatform=Windows`，Linux Node 不进入候选集。

如果要求 `UKey=Device-001`，Scheduler 只能选择当前持有该 UKey 且健康的节点。

## 11. 人工介入

- HumanIntervention：人工介入记录

HumanIntervention 至少包含类型（Captcha/QRCode/Face/Hardware/UKey/Manual）、状态、过期时间、Task/Execution/Step 关联、通知状态和一次性完成令牌摘要。

如果二维码或人脸认证需要用户操作，任务状态可以进入 `WaitingHuman`，而不是占用 Worker 无限等待。用户完成后通过安全事件唤醒原 Execution。

## 12. 硬件与 UKey

- HardwareDevice：执行节点发现的硬件设备
- HardwareProvider：硬件适配器定义
- HardwareBinding：业务系统/凭据/任务与硬件设备的绑定
- HardwareOperation：硬件操作记录

UKey 私钥、PIN 等机密数据不得进入数据库业务表；仅保存 Provider 引用、设备标识摘要、能力和健康状态。

UKey 属于 Node Capability + Resource Lock：

```text
Node-01
 └── UKey-001
      └── ResourceLock
```

同一时间默认只能被一个互斥 Execution 使用。

## 13. 验证码服务

- CaptchaProvider：第三方验证码服务配置
- CaptchaProviderRoute：业务系统到验证码 Provider 的路由策略
- CaptchaRequest：一次验证码识别请求

API Key/Secret 使用 CredentialRef，不直接保存明文。CaptchaRequest 保存计量、耗时、状态和错误摘要，不保存验证码原文。

## 14. 文件

- TaskFile：输入/输出文件
- Artifact：截图、二维码、下载文件、执行附件

文件元数据与物理存储解耦，未来可以支持本地磁盘、对象存储等 Provider。二维码属于短生命周期 Artifact。

## 15. 凭据

- Credential
- CredentialType
- CredentialAccessLog

凭据只允许通过 Credential Provider 获取，业务代码不得直接读取加密字段。

## 16. 通知

- NotificationProvider：通知渠道配置
- NotificationTemplate：消息模板
- NotificationPolicy：事件到用户/组织/渠道的路由策略
- NotificationMessage：一次实际发送消息
- NotificationDelivery：按渠道记录发送结果

支持 Email、DingTalk、WeChat、WeCom、WebSocket/站内通知，并为 SMS/Webhook 等预留 Provider 扩展点。Secret/AppKey 等配置通过 CredentialRef 管理。

## 17. 主题与语言

- ThemeProfile：主题配置
- Locale：语言定义
- TranslationResource：翻译资源
- UserPreference：用户个性化设置

主题和语言属于平台能力，不应散落在业务页面中硬编码。

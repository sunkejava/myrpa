# 一托 N 执行节点与异构环境调度设计

## 1. 目标

AgentRPA 必须支持：

> 一个中心服务器管理 N 台执行客户端，并根据业务系统要求自动把任务派发到合适的客户端。

客户端可以是：

- Windows 物理机
- Windows 虚拟机
- Windows 云桌面
- Linux 主机/容器
- 后续其他受支持运行环境

核心不是判断“这台机器是不是虚拟机”，而是判断：

> **这个执行节点是否具备运行该 BusinessSystem + Workflow 所需要的全部能力。**

## 2. 控制平面与执行平面

```text
                 AgentRPA Server
                Control Plane
                     │
       ┌─────────────┼─────────────┐
       ▼             ▼             ▼
   Node-001       Node-002      Node-00N
   Execution      Execution     Execution
   Plane          Plane         Plane
```

Server 负责：

- 用户、权限
- Agent/TaskPlan
- Workflow
- 任务队列
- Scheduler
- Node Registry
- Credential 授权
- 审计
- 监控

Node 负责：

- 浏览器/桌面环境
- Workflow 执行
- 本地文件
- UKey/证书
- 本地硬件
- 人工介入窗口
- 心跳和执行状态

## 3. Node 注册

Node 首次启动时向 Server 注册：

```text
POST /api/nodes/register
```

上报：

```json
{
  "name": "BJ-WIN-VM-01",
  "nodeKind": "VirtualMachine",
  "osPlatform": "Windows",
  "architecture": "x64",
  "agentVersion": "1.0.0",
  "capabilities": [
    "Browser.Chrome",
    "Browser.Edge",
    "DesktopUI",
    "Office.Excel"
  ],
  "networkZone": "GovernmentNetwork"
}
```

Server 返回 NodeId 和节点安全身份。后续心跳使用 NodeId + 节点凭据进行认证。

## 4. Node 能力模型

### 4.1 基础环境

```text
OsPlatform
NodeKind
Architecture
RuntimeVersion
```

### 4.2 浏览器

```text
Browser.Chrome
Browser.Edge
Browser.Firefox
```

可以进一步附带：

```text
Browser.Chrome@140
Browser.Edge@140
```

### 4.3 桌面能力

```text
DesktopUI
WindowsDesktop
Office.Excel
Office.Word
NativeApplication
```

如果网站必须使用 Windows 原生控件、IE 模式、UKey 中间件或桌面客户端，则必须声明对应能力。

### 4.4 硬件能力

```text
UKey
SmartCard
CertificateStore
Camera
Scanner
```

### 4.5 网络能力

```text
Network.Internet
Network.Intranet
Network.Government
Network.VPN.xxx
```

## 5. BusinessSystem 执行要求

业务系统维护默认执行要求：

```text
ExecutionRequirement
```

例如：

### 系统 A

```text
OS = Windows
NodeKind = VirtualMachine OR Physical
Browser = Edge
DesktopUI = false
```

### 系统 B

```text
OS = Windows
NodeKind = Physical
DesktopUI = true
UKey = required
NetworkZone = GovernmentNetwork
```

### 系统 C

```text
OS = Windows OR Linux
Browser = Chrome
DesktopUI = false
```

## 6. Workflow 继承要求

BusinessSystem 是默认要求，WorkflowVersion 可以增加更具体的要求。

```text
BusinessSystem
      ↓
WorkflowVersion
      ↓
Task
```

例如：

```text
社保系统
  OS = Windows
      ↓
社保缴费查询 Workflow
  Browser = Edge
      ↓
本次任务
  UKey = Device-001
```

最终要求：

```text
Windows + Edge + UKey(Device-001)
```

## 7. Scheduler 工作流程

```text
TaskQueued
    ↓
Resolve WorkflowVersion
    ↓
Build ExecutionRequirement
    ↓
Query Online Nodes
    ↓
Hard Filter
    ↓
Resource/Lease Check
    ↓
Score
    ↓
Acquire WorkerSlot
    ↓
Dispatch
    ↓
Node Execute
```

## 8. 硬过滤

以下条件必须全部满足：

```text
OS
NodeKind
Architecture
Browser
RequiredCapabilities
ForbiddenCapabilities
NetworkZone
NodePool
RequiredNodeIds
ExcludedNodeIds
RequiredHardwareIds
```

不满足的节点直接淘汰，不进入评分。

`PreferredNodeIds` 和 `CredentialAffinityKey` **不是硬过滤条件**：它们用于候选节点已经满足安全/能力边界后的软评分，避免因首选节点暂时不可用而错误地阻塞任务。

## 9. 软评分

通过硬过滤后，可以按照以下因素评分：

```text
PreferredNodeIds       +500
CredentialAffinityKey  +200
RequiredNodeIds        +1000（兼容现有强约束语义）
NodePool               +20
RequiredHardware       +50
Worker Load            +0~50
```

其中：

- `PreferredNodeIds`：业务明确偏好的执行节点，例如某城市固定登录环境。
- `CredentialAffinityKey`：凭据与执行环境的亲和标识，节点通过 `CredentialAffinity:{key}` 能力声明。
- 凭据亲和不替代 Credential 授权，也不意味着 Node 可以读取凭据；它只影响已经通过权限和能力检查后的节点排序。

Workflow 可以声明：

```json
{
  "executionRequirement": {
    "preferredNodeIds": ["00000000-0000-0000-0000-000000000001"],
    "credentialAffinityKey": "beijing-social-security"
  }
}
```

## 10. NodePool

建议支持执行节点池：

```text
NodePool
├── Windows-VM
├── Windows-Physical
├── Government-Network
├── Shanghai
└── Beijing
```

Node 可以属于多个逻辑标签，但一个节点池的授权和网络边界必须明确。

任务可以要求：

```text
NodePool = Beijing-Government
```

从而避免任务被调度到其他区域。

## 11. 一托 N 的任务拆分

例如用户上传 Excel，包含 100 条业务数据：

```text
Task-001
   │
   ├── Item-001 ──→ WinVM-01
   ├── Item-002 ──→ WinVM-01
   ├── ...
   ├── Item-030 ──→ WinVM-01
   ├── Item-031 ──→ WinVM-02
   ├── ...
   ├── Item-070 ──→ WinPC-01
   └── Item-100 ──→ WinPC-02
```

Scheduler 可以动态分配，不要求整个 Task 固定在一台机器。

如果业务要求整批必须使用同一登录会话，则 Workflow 可以声明：

```text
ExecutionAffinity = Task
```

此时整个 Task 绑定同一个 Node/WorkerSession。

## 12. 资源独占

UKey、桌面登录会话、特定浏览器 Profile 等资源不能简单视为普通能力。

它们应同时建模为：

```text
Capability + Resource
```

例如：

```text
Node-01
 └── UKey-001
       └── Exclusive Resource Lock
```

当 Execution 获得 UKey-001 后，其他任务不能同时使用该 UKey。

## 13. 节点离线与故障转移

节点必须持续发送心跳：

```text
Online
 ↓ heartbeat
Busy
 ↓ heartbeat lost
Unhealthy
 ↓ lease timeout
Offline
```

新任务：

```text
Offline Node → 不再派发
```

正在执行的任务：

```text
Node Offline
      ↓
判断最后执行步骤
      ↓
可安全重试？ ── Yes → 重新调度
      │
      No
      ↓
人工介入/失败
```

不能对已经完成“提交、缴费、申报”等不可逆动作的任务进行无条件自动重试，避免重复业务操作。

## 14. Human Intervention

二维码、短信、UKey PIN、刷脸等场景可以让 Node 暂停在：

```text
WaitingHuman
```

Server 通过通知中心发送：

- WebSocket/站内
- Email
- 钉钉
- 微信
- 企业微信
- 后续 SMS/Webhook

用户完成认证后：

```text
Human Completed
      ↓
Server Signal
      ↓
Node Resume
      ↓
Workflow Continue
```

二维码 Artifact 应设置 TTL 和一次性完成令牌。

## 15. 通信方式

初期推荐：

```text
Node → Server：HTTPS
Node ← Server：WebSocket
```

WebSocket 用于：

- 实时任务派发
- Task Cancel
- Task Pause
- Human Resume
- Node Command
- 心跳/状态推送

HTTPS 用于：

- 注册
- 文件上传下载
- 获取配置
- 获取任务详情
- 上报执行结果

后期节点规模增大后，可以在 Server 内部引入 RabbitMQ、Redis Streams 等消息基础设施，但 Node 协议本身保持稳定。

## 16. 安全边界

Node 必须遵循最小权限原则：

- Node 不能查询所有用户凭据
- Node 只能获取当前 Execution 授权的 Credential
- Node 不能自行创建 Task
- Node 不能修改 Workflow
- Node 不能修改 Permission
- Server 必须验证 Node 是否属于允许的 NodePool
- 每次派发都记录 NodeId、WorkerId、WorkflowVersion 和用户身份

## 17. 推荐实现顺序

### 第一阶段

先实现：

```text
ExecutionNode
NodeCapability
WorkerSlot
NodeLease
ExecutionRequirement
Scheduler
```

实现一台 Server + N 个 Windows Node。

### 第二阶段

增加：

```text
NodePool
PreferredNode
CredentialAffinity
UKey Resource Lock
Network Zone
```

其中 PreferredNode / CredentialAffinity 作为硬过滤后的软评分，不替代用户权限、凭据授权和硬件资源锁。

### 第三阶段

增加：

```text
Linux Node
Cloud Desktop
Remote VM Provider
自动扩缩容
```

这样不会因为第一版只支持 Windows，而把整个架构锁死。

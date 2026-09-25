# AgentRPA 权限模型

## 1. 目标

AgentRPA 的权限不能只控制菜单显示，必须控制实际业务执行范围。

核心授权维度：

```text
城市 → 系统 → 功能 → 操作
```

## 2. AccessPolicy

建议建立统一授权实体：

```text
AccessPolicy
├── SubjectType
├── SubjectId
├── CityId
├── SystemId
├── FunctionId
├── Actions
├── Effect
└── Conditions
```

Subject 可以是 User 或 Role。

## 3. 示例

```text
Role: 北京社保查询员

北京
└── 北京社保
    └── 社保缴费查询
        ├── View
        ├── Execute
        └── Export
```

同一个角色不能因为名称相同而自动拥有其他城市的权限。

## 4. 权限计算

请求：

```text
User = U001
City = BJ
System = SocialSecurity
Function = PaymentQuery
Action = Execute
```

Permission Engine 计算：

```text
用户直接授权
        OR
用户角色授权
        OR
组织授权
        ↓
显式 Deny 优先
        ↓
平台安全策略
        ↓
Allow / Deny
```

## 5. Agent 安全边界

Agent 可以提出：

```text
我要执行 BJ/SocialSecurity/PaymentQuery
```

但 Agent 不能决定：

```text
“我认为用户应该有权限，所以执行。”
```

最终结果必须由 Permission Engine 返回。

## 6. 权限缓存

权限可以缓存，但任务执行前必须使用有效版本或权限快照进行校验。

管理员撤销权限后，新任务不得继续使用旧授权。

对于已经运行的任务，可以按照安全策略选择：

- 立即终止
- 当前 Item 完成后终止
- 允许任务完成但禁止后续新任务

## 7. 数据范围

除功能权限外，可以继续增加业务数据范围：

```text
City
System
Function
Action
Organization
Department
DataScope
```

例如某用户拥有“北京社保缴费查询”，但只能查询本单位人员。

## 8. 高风险操作

申报、修改、删除等操作建议增加：

```text
Execute
 +
Confirm
 +
Audit
```

可要求二次确认、审批或双人复核。

## Workflow Step 业务动作预检

Workflow 的每个 Step 默认需要当前城市、业务系统与功能范围内的 `Execute` 权限。管理员可以在 Step 顶层增加 `requiredAction`（例如 `Approve`）；执行该 Step 时同时需要 `Execute` 和 `Approve`。`Condition.config.then/else`、`Loop.config.steps` 和 `SubWorkflow.config.steps` 都会在任务正式执行前遍历，即使某个运行时分支最后未命中也会预检。未识别的动作与畸形嵌套结构直接拒绝。

```json
{"steps":[{"type":"Click","requiredAction":"Approve","config":{"selector":"#approve"}}]}
```

任务创建、导入、入队、重试、Agent 提交、手工派发和后台自动派发均复核权限。管理员撤销权限后，未派发的任务不会继续派发；已派发并正在外部网站运行的 Step 尚不能中途撤销，后续需要执行节点的实时授权协议。此机制只覆盖业务动作：Capability、Agent、Tool、租户范围以及显式 Deny 尚需独立建模，不能据此视作完整权限链。

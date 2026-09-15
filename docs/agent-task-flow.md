# Agent 任务流设计

## 1. 目标

将用户自然语言稳定转换为可审计、可授权、可执行的 RPA 任务。Agent 不直接控制浏览器，而是生成结构化 TaskPlan，由权限引擎和 Workflow Engine 负责后续执行。

## 2. 标准链路

`Natural Language → Intent → TaskPlan → Permission Check → Confirmation → Queue → Workflow → RPA Engine → Result`

## 3. TaskPlan

核心字段：

- CityId：城市
- SystemId：业务系统
- FunctionId：业务功能
- Action：操作类型
- InputSources：文件、文本或结构化输入
- Parameters：业务参数
- OutputSpec：输出格式和目标
- WorkflowId / WorkflowVersion：确定性流程
- RiskLevel：风险等级
- RequiresConfirmation：是否需要用户确认

## 4. 规划原则

1. Agent 只负责理解和规划，不直接绕过权限。
2. 城市、系统、功能必须尽可能解析到明确 ID。
3. 无法确定目标时进入待确认状态，而不是猜测执行。
4. 高风险操作必须人工确认。
5. 每个 TaskPlan 必须保存版本，保证执行可追溯。
6. 批量任务拆成 Task + TaskItem，每个 Item 独立记录状态和重试次数。

## 5. 状态机

`Pending → Planning → PermissionChecking → WaitingConfirmation → Queued → Running`

运行中可进入 `WaitingHuman / WaitingCaptcha / Retrying`，最终进入 `Completed / Failed / Cancelled / Timeout / PermissionDenied`。

## 6. 示例

用户输入：

> 查询北京社保系统中人员信息.xlsx 所有人员 2026 年 8 月的社保缴费情况，并导出 Excel。

Agent 应解析为：

- 城市：北京
- 系统：北京社保
- 功能：社保缴费查询
- 输入：人员信息.xlsx
- 参数：2026-08
- 输出：Excel
- 操作：查询、导出

随后进行权限校验，选择对应 Workflow 版本，进入任务队列。

## 7. 异常

验证码、短信、UKey、扫码、人脸认证等无法自动处理时进入人工接管；网络超时、页面元素暂时不可用等可重试异常进入 Retry Policy。业务数据错误应标记具体 TaskItem，不应导致全部批次无条件失败。

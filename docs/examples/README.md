# 青岛社保模拟站点

在 `ASPNETCORE_ENVIRONMENT=Development` 下启动 API，访问 `/mock/qd-social-security`。模拟账号为 `demo`，密码为 `Demo123!`。生产环境不注册这些路由。数据仅保存在进程内存中，重启后清空。

`qd-social-security-add.json` 和 `qd-social-security-remove.json` 是现有 NodeAgent Playwright Runtime 可执行的 Workflow 定义示例。两份定义都声明 `adapter: qd-social-security`、节点能力 `Adapter:qd-social-security`；Workflow 使用 `@login.account` 等语义选择器，站点 CSS 统一由 `QingdaoSocialSecuritySiteAdapter` 映射。更换站点布局时实现并注册自己的 `IWorkflowSiteAdapter`，更新 Workflow 的 `adapter` 和节点 `executionRequirement.requiredCapabilities`，已有 `direct` 工作流继续使用原始 URL/CSS。将示例发布到相应业务功能，执行时传入必填参数 `mockBaseUrl`（如 `http://127.0.0.1:5000`）、`employeeName`、`idNumber`，身份证号参数标为敏感。站点仅从 NodeAgent 所在机器可访问时才能运行；容器环境请使用容器可访问的 API 地址。两种定义均标为 High 并要求任务审批；发生提交后异常时应先人工核验业务结果，不能盲目重跑。模拟账号仅供开发环境使用。

浏览器集成测试覆盖登录、证件校验、成功增员、重复增员、成功减员、重复减员；CI 另外启动真实 NodeAgent，验证审批后增员与减员 Workflow 的连续派发、浏览器提交、Step 检查点，并通过需要模拟站点登录的 `/mock/qd-social-security/employees/status?idNumber=...` 核对两次提交后的参保状态。模拟服务尚未实现真实外部系统自动对账或持久化。

若高风险执行失败，独立管理员可在「核验中心」查看最后的 Step 检查点，先到外部站点核对状态，再记录外部查询单号或截图编号。`Submitted` 结案且不重新提交；`NotSubmitted` 仅在任务仍有重试次数时重新入队。每次决定写入原 Execution 的 `ManualReconciliation` 日志，已处理的执行不能再次核验。这个入口依赖人工实际核对，不能代替真实站点的自动对账。

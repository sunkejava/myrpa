# 青岛社保模拟站点

在 `ASPNETCORE_ENVIRONMENT=Development` 下启动 API，访问 `/mock/qd-social-security`。模拟账号为 `demo`，密码为 `Demo123!`。生产环境不注册这些路由。数据仅保存在进程内存中，重启后清空。

`qd-social-security-add.json` 和 `qd-social-security-remove.json` 是现有 NodeAgent Playwright Runtime 可执行的 Workflow 定义示例。将其发布到相应业务功能，执行时传入必填参数 `mockBaseUrl`（如 `http://127.0.0.1:5000`）、`employeeName`、`idNumber`，身份证号参数标为敏感。站点仅从 NodeAgent 所在机器可访问时才能运行；容器环境请使用容器可访问的 API 地址。两种定义均标为 High 并要求任务审批；发生提交后异常时应先人工核验业务结果，不能盲目重跑。模拟账号仅供开发环境使用。

浏览器集成测试覆盖登录、证件校验、成功增员、重复增员、成功减员、重复减员；CI 另外启动真实 NodeAgent，验证审批后增员与减员 Workflow 的连续派发、浏览器提交、Step 检查点，并通过需要模拟站点登录的 `/mock/qd-social-security/employees/status?idNumber=...` 核对两次提交后的参保状态。模拟服务尚未实现真实外部系统对账、持久化或可替换的外部站点 Adapter。

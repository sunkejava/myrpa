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

## 6. 执行

- Task：一次用户任务
- TaskItem：批量任务中的单条业务数据
- Execution：一次实际 Workflow 执行
- ExecutionStep：执行步骤记录
- RetryRecord：重试记录
- HumanIntervention：人工介入记录

HumanIntervention 至少包含类型（Captcha/QRCode/Face/Hardware/UKey/Manual）、状态、过期时间、Task/Execution/Step 关联、通知状态和一次性完成令牌摘要。

## 7. 硬件与 UKey

- HardwareDevice：执行节点发现的硬件设备
- HardwareProvider：硬件适配器定义
- HardwareBinding：业务系统/凭据/任务与硬件设备的绑定
- HardwareOperation：硬件操作记录

UKey 私钥、PIN 等机密数据不得进入数据库业务表；仅保存 Provider 引用、设备标识摘要、能力和健康状态。

## 8. 验证码服务

- CaptchaProvider：第三方验证码服务配置
- CaptchaProviderRoute：业务系统到验证码 Provider 的路由策略
- CaptchaRequest：一次验证码识别请求

API Key/Secret 使用 CredentialRef，不直接保存明文。CaptchaRequest 保存计量、耗时、状态和错误摘要，不保存验证码原文。

## 9. 文件

- TaskFile：输入/输出文件
- Artifact：截图、二维码、下载文件、执行附件

文件元数据与物理存储解耦，未来可以支持本地磁盘、对象存储等 Provider。二维码属于短生命周期 Artifact。

## 10. 凭据

- Credential
- CredentialType
- CredentialAccessLog

凭据只允许通过 Credential Provider 获取，业务代码不得直接读取加密字段。

## 11. 通知

- NotificationProvider：通知渠道配置
- NotificationTemplate：消息模板
- NotificationPolicy：事件到用户/组织/渠道的路由策略
- NotificationMessage：一次实际发送消息
- NotificationDelivery：按渠道记录发送结果

支持 Email、DingTalk、WeChat、WeCom、WebSocket/站内通知，并为 SMS/Webhook 等预留 Provider 扩展点。Secret/AppKey 等配置通过 CredentialRef 管理。

## 12. 主题与语言

- ThemeProfile：主题配置
- Locale：语言定义
- TranslationResource：翻译资源
- UserPreference：用户个性化设置

主题和语言属于平台能力，不应散落在业务页面中硬编码。

# 安全与凭据设计

## 1. 安全边界

AgentRPA 必须遵循最小权限原则。Agent 不得通过自然语言、Workflow 参数或浏览器脚本绕过 Permission Engine。

执行前必须完成：

`用户身份 → 城市授权 → 系统授权 → 功能授权 → Action 授权`

## 2. 凭据中心

支持用户名密码、Token、Cookie、证书、USB Key/UKey 等凭据。凭据与业务 Workflow 解耦。

密码、Token、Cookie、证书私钥等敏感信息必须加密存储，前端原则上只获得脱敏信息或引用 ID。执行器只能通过 Credential Provider 获取当前任务被授权使用的凭据。

### 2.1 UKey / USB Key

为兼容社保、公积金、医保、政务等需要实体介质的业务系统，平台必须提供统一 `IHardwareCredentialProvider` 抽象，不允许业务 Workflow 直接依赖具体 UKey 厂商 SDK。

支持：

- Windows 本机 UKey/USB Key 自动发现
- 厂商驱动/SDK/COM/PKCS#11/CSP 等适配
- UKey 插拔检测和设备状态监控
- 按任务/账号/业务系统绑定 UKey
- 多 UKey 并存时按授权上下文选择设备
- 登录签名/证书操作由受控 Provider 执行
- UKey 不可用时进入 `WaitingHardware`，支持人工处理后恢复
- 记录 UKey Provider、设备标识摘要、操作结果和审计信息，但不得记录私钥和 PIN

平台应预留远程硬件代理能力，使浏览器 Worker 与 UKey 所在机器可以分离部署；远程代理必须经过双向认证和任务授权。

## 3. 验证码与第三方识别服务

平台需要兼容第三方验证码识别服务，但验证码服务必须作为独立 Provider，不与具体业务系统 Workflow 强耦合。

统一抽象：

```text
ICaptchaProvider
├── ImageCaptchaProvider
├── SliderCaptchaProvider
├── ClickCaptchaProvider
└── ThirdPartyCaptchaProvider
```

要求：

- 支持多个供应商配置和故障切换
- 支持 API Key/Secret 加密保存
- 支持超时、重试、限流和余额/健康状态
- 根据业务系统配置选择 Provider
- 验证码图片原则上只在内存或受控临时存储中处理
- 第三方服务调用必须进入审计/计量日志，但不得记录验证码原文
- 对第三方服务增加域名白名单和网络访问策略
- 验证码识别失败可转人工接管

第三方验证码识别必须符合目标业务系统、服务商和适用法律法规的授权要求；对于明确禁止自动化或需要人工安全验证的场景，必须转人工，不应通过技术手段绕过安全控制。

## 4. 扫码登录 / 二维码人工接管

对于登录需要扫码、二维码确认或移动端授权的业务系统，Workflow 应支持 `HumanIntervention`。

执行器检测到二维码后：

```text
Workflow
 ↓
生成/捕获二维码
 ↓
Task → WaitingHuman
 ↓
Notification Service 推送二维码
 ↓
用户扫码/确认
 ↓
Worker 检测登录状态
 ↓
Task → Running
```

平台需要提供统一二维码人工介入接口：

```text
POST /api/tasks/{taskId}/interventions
GET  /api/tasks/{taskId}/interventions/{id}
POST /api/tasks/{taskId}/interventions/{id}/complete
```

通知消息应支持二维码图片附件/临时安全 URL。二维码有效期、一次性使用、任务绑定和过期失效必须由平台控制。

禁止把长期有效的登录二维码公开暴露到日志、普通文件目录或无鉴权 URL。

## 5. 人脸识别 / 实名认证

人脸识别、活体检测等强身份认证属于高风险人工介入场景。平台可以提供接口用于：

- 检测 Workflow 进入人脸认证步骤
- 创建人工认证请求
- 通过邮件/钉钉/微信/企业微信等渠道通知用户
- 提供认证页面或第三方授权链接
- 接收认证完成回调
- 恢复原 Browser Session/Workflow

平台本身不应保存用户生物特征模板，不应通过模拟人脸、伪造活体等方式绕过认证。

## 6. 通知中心

平台提供统一 Notification Service，屏蔽不同通知渠道的差异。

支持渠道：

- Email
- DingTalk（钉钉）
- WeChat（微信/公众号等按实际能力配置）
- WeCom（企业微信）
- WebSocket/站内通知
- 后续可扩展 SMS、Webhook 等

统一接口：

```text
INotificationProvider
├── EmailNotificationProvider
├── DingTalkNotificationProvider
├── WeChatNotificationProvider
├── WeComNotificationProvider
└── WebhookNotificationProvider
```

通知配置至少包含：

```text
Provider
Name
Enabled
Endpoint/AppId/AppKey 等配置
Secret/CredentialRef
Timeout
RetryPolicy
RateLimit
TemplateSet
```

敏感配置必须通过 Credential Provider 保存，前端不得返回 Secret 明文。

### 6.1 通知场景

统一支持：

- 任务创建/开始
- 任务完成
- 任务失败
- 批量任务部分失败
- 需要人工接管
- 二维码扫码
- 人脸/实名认证待处理
- UKey 缺失
- Workflow 发布/停用
- Worker 离线
- 系统健康检查异常

### 6.2 通知模板

通知模板不得散落在业务代码中，应支持：

```text
TemplateCode
Channel
TitleTemplate
BodyTemplate
Variables
Enabled
```

例如：

```text
RPA_HUMAN_INTERVENTION
RPA_TASK_COMPLETED
RPA_TASK_FAILED
RPA_QR_CODE_REQUIRED
RPA_UKEY_REQUIRED
```

系统应支持按用户、组织、任务、业务系统配置通知策略，并允许一个事件同时发送多个渠道。

## 7. 日志脱敏

日志不得记录完整密码、验证码、Cookie、Access Token、身份证号、银行卡号、UKey PIN 等敏感数据。身份证等业务字段按统一 SensitiveDataMasker 规则脱敏。

## 8. 人工接管

人工介入页面只展示完成当前任务所必需的信息。人工操作产生的事件必须进入审计日志，恢复自动执行时重新校验任务状态、授权上下文和 Intervention 有效期。

## 9. 审计

记录用户、时间、IP/设备标识、城市、系统、功能、Action、TaskId、WorkflowVersion、结果和异常摘要。审计记录原则上不可由普通管理员修改。

## 10. Workflow 安全

Script Step 必须限制能力和执行上下文，不允许默认访问服务器文件系统、环境变量、进程、网络凭据等高风险资源。外部 URL 应经过系统白名单或业务系统配置校验。

## 11. 数据生命周期

输入文件、截图、二维码、下载文件和执行结果必须支持保留期限配置。二维码、人工认证链接等短期凭证应采用更短 TTL，并在任务完成、取消或过期后立即失效。过期数据自动清理；审计记录和业务结果按照组织策略分别管理。

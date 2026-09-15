# AgentRPA 外部集成与人工认证能力设计

## 1. 目标

AgentRPA 面向真实政务、社保、医保、公积金及企业业务自动化时，必须兼容 UKey/USB Key、第三方验证码识别、扫码登录、人脸/实名人工认证，以及 Email、钉钉、微信、企业微信等通知能力。

这些能力统一采用 Provider/Adapter 架构，业务 Workflow 不直接绑定供应商 SDK。

## 2. LLM Provider

Agent 通过 `ILlmProvider` 访问模型，不把模型 SDK、Endpoint 或厂商协议写死在 Agent 核心。

第一阶段提供 OpenAI Compatible Provider，可直接连接 llama.cpp server 等本地推理服务：

```text
Agent Core
   ↓
ILlmProvider
   ↓
OpenAiCompatibleLlmProvider
   ↓
/v1/chat/completions
   ↓
本地模型 / 云端 OpenAI-Compatible 服务
```

配置项：`AgentRPA:Llm:Endpoint / Model / ApiKey / Timeout`。API Key 只用于 Provider 请求，不写入普通日志。Provider 返回可选的输入/输出 Token 统计，为后续 Agent 成本和性能统计预留接口。

## 3. UKey / USB Key

RPA Worker 应支持 Windows 执行节点自动发现 UKey，并通过统一 `IHardwareCredentialProvider` 抽象访问厂商 SDK、COM、CSP/KSP、PKCS#11 等能力。

支持设备发现、插拔检测、健康状态、任务/账号绑定、多设备选择、签名/证书操作，以及 UKey 缺失时进入 `WaitingHardware` 并通知用户。私钥和 PIN 永不进入数据库或普通日志。

应预留远程硬件代理，使 Browser Worker 与 UKey 所在机器可以分离部署；远程代理必须双向认证并校验任务授权。

```csharp
public interface IHardwareCredentialProvider
{
    Task<IReadOnlyList<HardwareDevice>> DiscoverAsync(CancellationToken cancellationToken);
    Task<HardwareDeviceStatus> GetStatusAsync(string deviceId, CancellationToken cancellationToken);
    Task<HardwareOperationResult> ExecuteAsync(HardwareOperationRequest request, CancellationToken cancellationToken);
}
```

## 4. 验证码服务

第三方验证码识别通过 `ICaptchaProvider` 接入，不允许 Workflow 写死 API Key。支持 Provider 路由、主备切换、超时、重试、限流、健康检查和调用计量。

```csharp
public interface ICaptchaProvider
{
    Task<CaptchaResult> RecognizeAsync(CaptchaRequest request, CancellationToken cancellationToken);
}
```

验证码识别必须符合目标业务系统、服务商和适用法律法规的授权要求；明确禁止自动化或要求人工安全验证的场景转人工，不绕过安全控制。

## 5. 扫码登录 / 二维码人工接管

Worker 检测到登录二维码后：

```text
Running → QRCodeDetected → Create HumanIntervention
        → 保存短期 Artifact → Notification Service
        → 用户收到二维码并扫码 → Worker 检测登录状态
        → 恢复 Browser Session / Workflow
```

二维码必须绑定 `TaskId + InterventionId`，具有短 TTL，完成、取消或过期立即失效。通知支持图片附件或一次性安全查看地址，禁止公开永久 URL。

建议接口：

```text
POST /api/tasks/{taskId}/interventions
GET  /api/tasks/{taskId}/interventions/{id}
POST /api/tasks/{taskId}/interventions/{id}/complete
```

## 6. 人脸 / 实名认证

需要人脸、活体或实名认证时进入 `WaitingAuthentication`/`WaitingHuman`。平台通过通知渠道发送安全认证入口或官方第三方认证链接，并通过回调、状态查询或用户确认恢复任务。

平台不保存生物特征模板，不通过伪造人脸、模拟活体等方式绕过认证。

## 7. 通知中心

统一 `INotificationProvider`，第一阶段支持：

- Email
- DingTalk（钉钉）
- WeChat（微信）
- WeCom（企业微信）
- WebSocket/站内通知

预留 SMS、Webhook 等 Provider。

```csharp
public interface INotificationProvider
{
    string Channel { get; }
    Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}
```

NotificationPolicy 支持一个事件同时发送多个渠道，例如 UKey 缺失可以同时发送站内通知、企业微信和 Email。

通知配置包含 Provider、名称、启用状态、Endpoint/AppId/AppKey、CredentialRef、超时、重试、限流和模板集。Secret 等敏感配置必须由 Credential Provider 管理。

## 8. 通知事件

至少支持：

- 任务创建/开始/完成/失败
- 批量任务部分失败
- 需要人工接管
- 二维码扫码
- 人脸/实名认证待处理
- UKey 缺失或恢复
- Workflow 发布/停用
- Worker 离线
- 业务系统健康检查异常

通知模板独立管理，支持 `TemplateCode / Channel / TitleTemplate / BodyTemplate / Variables / Enabled`。通知失败进入独立重试队列，不应直接导致业务 Workflow 失败。

## 9. 安全与权限

通知配置、验证码 Provider、硬件 Provider、人工介入均属于受保护资源，建议权限：

```text
Notification: View / Configure / Test
CaptchaProvider: View / Configure / Test
Hardware: View / Configure / Operate
Intervention: View / Complete / Cancel
```

业务任务仍必须执行 `City → System → Function → Action` 权限校验。

## 10. 审计

外部 Provider 调用至少记录 ProviderType、ProviderId、TaskId、ExecutionId、StepId、StartTime、Duration、Status、ErrorCode。禁止记录密码、Token、验证码原文、UKey PIN、证书私钥和生物特征数据。

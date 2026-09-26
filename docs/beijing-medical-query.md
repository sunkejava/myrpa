# 北京医保人员查询与下载：配置和执行

更新并重启 API 后，默认资源初始化会自动提供“中国 → 北京市 → 北京市 → 北京医保业务系统 → 人员信息查询与下载”目录，开发和生产环境均可见。初始化可重复执行；不会预置真实政务网址、账号、凭据、客户信息或可直接执行的工作流。完整目录和关联说明参见 [默认资源与工作流配置](default-resources-and-workflows.md)。

1. **确认接入条件。** 取得目标系统的正式入口、业务授权、测试环境与稳定的输入框/下载按钮定位；确认要用的网络区域、登录方式和 UKey。客户数据须获合法授权；不要把账号、密码、身份证明文件、验证码写进工作流 JSON、日志或 URL。
2. **配置资源与权限。** 在“资源管理”确认北京城市、医保系统和人员查询功能；在“权限中心”为操作者授予该城市/系统/功能的 Execute 权限。如需审批敏感操作，同时设置对应步骤的 requiredAction，并配置独立审批角色。
3. **准备节点。** 启动可访问目标系统的 Windows NodeAgent，配置唯一 AgentKey、注册密钥、网络区域、Edge/浏览器能力及执行槽位；在“节点管理”审核并检查心跳在线。UKey 应保持客户之间物理或权限隔离；离线后重新连接会恢复节点在线，已失败的执行仍需人工核对。
4. **建立工作流。** 在“工作流管理”选择“人员信息查询与下载”对应功能，创建工作流，添加版本并发布。下列 JSON 为结构示例：需要用获授权的真实地址和经测试的页面选择器替换占位值，不能原样运行。登录动作可能需要先执行 HumanTask 让人完成扫码、验证码或 UKey 授权。

```json
{
  "version": 1,
  "riskLevel": "High",
  "requiresApproval": true,
  "parameters": {
    "customerCode": { "type": "string", "required": true, "maxLength": 64 },
    "personId": { "type": "string", "required": true, "sensitive": true, "maxLength": 64 }
  },
  "steps": [
    { "id": "open", "type": "Navigate", "config": { "url": "{{systemBaseUrl}}" } },
    { "id": "login", "type": "HumanTask" },
    { "id": "input-person", "type": "Input", "config": { "selector": "[data-testid=person-id]", "value": "{{personId}}" } },
    { "id": "query", "type": "Click", "config": { "selector": "[data-testid=query]" } },
    { "id": "wait-result", "type": "WaitForElement", "config": { "selector": "[data-testid=query-result]" } },
    { "id": "download", "type": "Download", "config": { "selector": "[data-testid=export]", "path": "artifacts/person-result.xlsx" } },
    { "id": "end", "type": "End" }
  ]
}
```

5. **创建任务。** 在任务规划页面选中已发布工作流版本，按客户分别提交任务；每个 `items` 元素是一名客户下的一条待查询记录，例如 `{"customerCode":"customer-a","personId":"TEST-ID-001"}`。批量文件导入也会按行生成任务项。任务经审批后入队；每个任务项在独立执行中查询并登记下载产物，在任务详情选择执行后通过“下载”按钮取回。不同客户需要不同登录身份、授权或出口时，分别配置节点池、访问权限和任务，避免同一浏览器会话交叉使用。
6. **处理人工介入与失败。** 执行到 HumanTask 时自动生成待处理记录，任务详情中可打开对应介入，核验后点击完成，Agent 在原浏览器会话中继续。二维码一次性令牌须由有权处理的人员消费；超时或节点离线租约到期需人工核对外部业务状态，不能直接重放下载等非幂等步骤。

当前执行器支持输入、查询、等待、下载及人工暂停；“Extract”目前只记入经脱敏的执行日志，尚无结构化结果字段或按客户隔离的文件归档。生产级医保接入还需要真实页面适配器、凭据托管、数据保护与授权范围设计、客户级隔离及验收测试；此示例不代表已经连通北京医保线上系统。

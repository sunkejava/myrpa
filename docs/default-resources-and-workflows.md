# 默认资源与工作流配置

## 为什么以前页面没有默认资源

此前城市/系统的演示种子数据只在执行 `dotnet run -- --seed-only` 时写入；普通 API 启动不会执行。现在 API **每次启动**在数据库迁移后补齐基础资源目录。使用已有数据库时，更新代码并**重启 API**，再刷新前端的“城市与系统”页面即可；不会删除数据库，也不会覆盖已有系统的地址、启停状态或工作流。确保 API 指向你正在查看的数据库：连接字符串中的 SQLite 相对路径取决于 API 的进程工作目录。开发专用 `scripts/seed-development.sh` 仍可用于额外的演示资源。

| 国家 | 省份 | 城市 | 区县 |
| --- | --- | --- | --- |
| 中国 `CN` | 北京市 `BJ` | 北京市 `CN-BJ` | 东城区、西城区、朝阳区、海淀区 |
| 中国 `CN` | 山东省 `SD` | 青岛市 `CN-SD-QD` | 市南区、市北区、崂山区 |

两座城市分别预置社保、医保、公积金三个业务系统（编码为 `BJ-SOCIAL` / `BJ-MEDICAL` / `BJ-HOUSING` 和 `QD-SOCIAL` / `QD-MEDICAL` / `QD-HOUSING`）。每个系统预置人员增加 `PERSON-ADD`、人员减少 `PERSON-REMOVE`、人员花名册 `PERSON-ROSTER`、单位参保证明 `UNIT-CERTIFICATE`、个人参保证明 `PERSON-CERTIFICATE` 五个功能。北京医保另保留 `PERSON-QUERY`（人员信息查询与下载）。这些是**分类目录**，不代表已对接外部社保/医保/公积金平台；系统地址默认留空。

生产环境也会补齐上述目录，因此不需要用开发专用脚本初始化生产库；上线前应按实际授权范围停用不适用的资源。

## 从资源到可执行工作流

系统的关联关系是：`国家 → 省份 → 城市 → 业务系统 → 业务功能 → 工作流 → 工作流版本 → 任务 → 任务项 → 执行与下载产物`。区县是城市下的分类，当前任务权限以**城市/系统/功能**为范围，区县不会自动限制任务访问。创建工作流只需要选择**城市、系统、业务功能**，无需绑定区县。不同城市的同名系统/功能有独立 ID，创建工作流时务必选对城市。

1. 在“城市与系统”选择国家、省份和城市，点业务系统表格的“配置地址”，填入获授权的完整 HTTP/HTTPS 地址并保存。默认目录不附带真实平台网址；不要把账号、密码写进系统地址或工作流 JSON。
2. 进入“Workflow 管理”，点“新增 Workflow”，依次选择城市、业务系统、业务功能，填写名称与说明并创建。后台使用所选业务功能的 ID 绑定资源；一个业务功能可以建立多个不同用途的工作流。
3. 选中刚创建的工作流，添加 Navigate、Input、Click、WaitForElement、Download 等步骤，必要时加入 HumanTask 等待人工登录。打开“定义 JSON”，将 Navigate 的 `config.url` 写成 `{{systemBaseUrl}}`（或在后面追加真实路径）。这个变量在**派发时**由关联业务系统的地址提供，任务项里同名字段不能覆盖。系统地址未配置时，引用该变量的任务会保持等待资源，不会误打开空地址。
4. 点击“保存并发布版本”。每次保存生成新版本；既有任务继续使用创建时指定的版本。业务系统、所在城市或其上级区域停用时，权限复核会阻止后续执行。需要为操作者在“权限中心”授权该城市、系统、功能的 Execute；敏感步骤或工作流还需要审批。
5. 在“AI 工作台”提交任务，选择已发布的工作流与版本；多个客户按任务或任务项分别传入参数。审批后入队，在“任务中心”查看执行日志及下载产物；HumanTask 在“人工介入”确认。涉及客户登录身份和数据时，应按客户隔离节点/凭据/产物，正式上线前验证真实系统的授权、选择器与业务结果。

### 工作流定义示例（需替换页面选择器）

下方是北京医保 `PERSON-QUERY` 的最小示意。`{{personId}}` 来自任务项，`{{systemBaseUrl}}` 来自业务系统资源，选择器必须经目标测试环境验证后才能运行。

```json
{
  "riskLevel": "High",
  "requiresApproval": true,
  "parameters": { "personId": { "type": "string", "required": true, "sensitive": true } },
  "steps": [
    { "id": "open", "type": "Navigate", "config": { "url": "{{systemBaseUrl}}" } },
    { "id": "login", "type": "HumanTask" },
    { "id": "input", "type": "Input", "config": { "selector": "[data-testid=person-id]", "value": "{{personId}}" } },
    { "id": "query", "type": "Click", "config": { "selector": "[data-testid=query]" } },
    { "id": "result", "type": "WaitForElement", "config": { "selector": "[data-testid=result]" } },
    { "id": "download", "type": "Download", "config": { "selector": "[data-testid=export]", "path": "artifacts/person-result.xlsx" } },
    { "id": "end", "type": "End" }
  ]
}
```

对应单条任务项内容为 `{"personId":"TEST-ID-001"}`。具体的任务创建、审批及产物下载流程参见 [北京医保查询配置说明](beijing-medical-query.md)。**不会自动生成可运行的默认工作流**：缺少获授权的真实平台地址、页面定位和登录方式时发布“可执行”模板会误导调度和业务人员。

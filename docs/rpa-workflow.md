# Workflow 节点手册与操作指南

本文以当前 `.NET 10 API + NodeAgent PlaywrightWorkflowRuntime` 实际支持的行为为准。需求规格中的 `Login`、`OpenPage`、`ExecuteScript` 等名称并非可直接发布的节点类型；请使用下表的类型及 `config` 结构。默认示例在“Workflow 管理 → 默认工作流示例”展示，载入仅生成草稿，不会自动连接政务平台或发布。

## 工作流如何依赖资源

`国家 → 省份 → 城市 → 业务系统 → 业务功能 → Workflow → Version → Task → Item → Execution`。创建 Workflow 时必须选择城市、系统、功能；任务按已发布版本执行。任务操作人须拥有对应城市/系统/功能的 `Execute` 权限，声明了 `requiredAction` 的节点还需相应业务动作权限。区县目前只作为城市下的资源目录，不参与任务权限隔离。

“城市与系统”里系统地址默认空白，管理员填写获授权的 HTTP/HTTPS 入口。节点的 `Navigate.config.url` 可写 `{{systemBaseUrl}}` 或 `{{systemBaseUrl}}/path`；派发时由关联业务系统的地址注入，任务项不能伪造同名参数。引用了这个变量却未配置系统地址的任务保持 `WaitingForResource`。模板示意用的 `replace-*` 选择器须按目标环境更换，设计器在未更换时阻止发布。

## 定义 JSON 结构

```json
{
  "riskLevel": "High",
  "requiresApproval": true,
  "parameters": {
    "personId": { "type": "string", "required": true, "sensitive": true, "maxLength": 64 }
  },
  "steps": [
    { "id": "open", "type": "Navigate", "config": { "url": "{{systemBaseUrl}}" } },
    { "id": "login", "type": "HumanTask" },
    { "id": "person", "type": "Input", "config": { "selector": "[data-testid=person-id]", "value": "{{personId}}" } },
    { "id": "result", "type": "WaitForElement", "timeoutMs": 30000, "retryCount": 1, "config": { "selector": "[data-testid=result]" } },
    { "id": "finish", "type": "End" }
  ]
}
```

`parameters` 支持 `string`、`integer`、`number`、`boolean`、`object`、`array`，可声明 `required`、`default`、`sensitive`、`minLength/maxLength`、`minimum/maximum`、`enum`。任务创建、批量导入和入队时均校验参数；任务项使用 JSON 对象，例如 `{"personId":"TEST-ID-001"}`。当前替换语法为 `{{personId}}`；嵌套对象的 `{{item.personId}}`、`{{system.baseUrl}}` **不会解析**。服务端检查必填参数，但未自动从业务页面抓取参数。

`riskLevel` 为 `Low/Medium/High/Critical`；`requiresApproval:true`、高风险等级、节点 `requiresApproval:true` 或节点动作 `Approve` 会开启**任务级**审批。`requiredAction` 可填写 `View/Execute/Create/Update/Delete/Approve/Manage/Export/Download/Upload`，它是在基础 Execute 权限之外额外检查。服务端未提供每个高风险节点运行时的单独审批。`executionRequirement` 可声明调度能力，例如 `{"requiredCapabilities":["Adapter:qd-social-security"]}`；节点需上报匹配能力。

## 节点类型与配置

| 类型 | `config` 必需/常用字段 | 实际效果与注意点 |
| --- | --- | --- |
| `Navigate` | `url` | 在当前浏览器页打开地址；支持 `{{systemBaseUrl}}` 和任务参数。 |
| `Click` | `selector` | 点击元素；可能造成外部提交，不自动重试。 |
| `Input` | `selector`, `value` | 向输入框填入替换后的字符串；不要在定义中硬编码真实账号或密码。 |
| `Select` | `selector`, `value` | 按 HTML `option` 的 value 选择；不是自定义下拉框的通用方案。 |
| `Wait` | `milliseconds`（默认 500） | 延迟 0–120000 毫秒，仍受步骤超时约束。 |
| `WaitForElement` | `selector`, 可选 `timeout` | 等待元素出现；适合查询结果加载。 |
| `Extract` | `selector`, `output` | 将文本保存为结构化结果字段，并允许后续步骤使用 `{{字段名}}`；结果只在任务所属用户的任务详情中返回，不写入执行进度日志。单字段最多 16384 字符。 |
| `Assert` | `selector`, `contains` | 元素文本不包含期望值时失败；仅作为只读断言。 |
| `Screenshot` | `path`（默认 `artifacts/{id}.png`）, `fullPage`（默认 true） | 节点生成截图后上传到服务端产物存储，校验 SHA256 和长度；任务所属用户可下载，单文件最大 50 MiB。 |
| `Download` | `selector`, `path`, 可选 `timeout` | 等待点击产生浏览器下载，并保存到节点文件路径；属于有副作用的步骤，禁止自动重试。 |
| `Upload` | `selector`, `path` | 将 NodeAgent 本地已存在的文件设为上传项；输入路径不是浏览器电脑上的文件。 |
| `Condition` | `selector`, `contains`, `then` 和/或 `else` 步骤数组 | 元素文本包含指定字符串则执行 then，否则执行 else。 |
| `Loop` | `count`（0–1000）, `steps` 数组 | 将内嵌步骤重复指定次数；不支持基于表格行的自动遍历。 |
| `SubWorkflow` | `steps` 数组 | 执行本定义中的内联步骤；**尚不支持**按其他 Workflow ID 跨工作流调用。 |
| `HumanTask` | 可为空；`interventionType` 可选 Captcha、FaceAuthentication、UKeyConfirmation、ManualApproval；`title` 可选 | 报告 `WaitingForHuman`，服务端生成相应类型待处理记录；所有者确认后在**原浏览器会话**继续。该确认流程不执行厂商 UKey 签名，也不提供远端浏览器可视接管或验证码自动识别。 |
| `End` | 可为空 | 结束当前级别的步骤数组；作为末尾节点使用。 |
| `Script` | — | 发布校验会拒绝，受控脚本 Provider 尚未实现。 |

节点需有 `type`；建议明确填写唯一 `id`，便于日志定位。需要 selector/url/value/path 的节点将其写在 `config` 下，不要写在 Step 顶层。普通 CSS 定位符可直接使用；`@login.account` 等语义定位符仅在适配器注册并匹配节点能力时可用。示例适配器 `qd-social-security` 仅用于开发模拟站点；默认适配器为 `direct`。

### 节点本地站点适配配置

在获授权的测试节点的 `NodeAgent:SiteAdapters` 配置具体站点。配置只保存在节点本地，不将真实账号、口令、验证码或 UKey PIN 放在工作流定义里。示例中的地址和 CSS 定位符须由实际测试站点提供：

```json
{
  "NodeAgent": {
    "SiteAdapters": [{
      "Code": "beijing-medical-test",
      "BaseUrl": "https://example.invalid/medical/",
      "Paths": { "@site.login": "login" },
      "Selectors": { "@person.id": "[name=personId]", "@person.result": "[data-testid=person-result]" }
    }]
  }
}
```

节点启动时校验编码、地址和映射并自动上报 `Adapter:beijing-medical-test` 能力；重复编码或试图把页面映射到其他主机时拒绝启动。工作流根节点填 `"adapter":"beijing-medical-test"`，同时声明 `"executionRequirement":{"requiredCapabilities":["Adapter:beijing-medical-test"]}`；Navigate 使用 `"url":"@site.login"`，Input/Extract 使用配置中的语义选择器。管理员仍需将系统资源中的地址配置为获授权的真实地址，并对登录、查询结果、下载与权限逐项验收。配置中的示例域名不可访问，也不是北京医保生产接口。

`timeoutMs` 范围 100–120000，默认 30000。仅 `Navigate`、`WaitForElement`、`Assert`、`Extract` 可用 `retryCount`（0–3）；Click/Upload/Download/HumanTask 不能通过步骤重试绕过外部幂等要求。嵌套深度最多 8 层，每个数组最多 1000 步。任务级“重试失败”仅允许无需审批的只读流程自动重试；提交过外部业务的任务必须先核验外部状态。

## 默认示例展示和使用

“Workflow 管理”顶部展示：北京医保人员查询草稿、单位参保证明草稿；**仅在开发模式**展示青岛社保模拟增员/减员示例。每张卡片列出依赖资源、节点预览与前置条件。点击“载入为草稿”后：

1. 若当前未选择 Workflow，创建对话框会预填名称，需**手动选择**相符城市/系统/功能后创建；不会自动给用户授权或发布。
2. 若已经选择 Workflow，模板只替换当前编辑框的定义草稿；请核对该 Workflow 已绑定的业务功能，避免把医保定义发布到公积金功能。
3. 逐节点编辑 ID、按节点类型显示的页面地址/选择器/输入值/文件路径等字段、配置 JSON、超时、只读重试次数和动作权限；节点可上下移动或删除。条件分支、循环和内联子流程都有独立的嵌套步骤编辑区；高级 JSON 编辑用于完整参数 Schema 和执行环境限制。先更新系统地址和实际选择器，再创建并发布新版本。
4. 为操作人授权，必要时由另一管理员审批；在任务规划页选择已发布版本，传入参数，提交与入队。任务中心可看步骤日志、人工介入和下载产物。

开发模拟增减员模板的参数为 `mockBaseUrl`、`employeeName`、`idNumber`、`submissionId`，NodeAgent 需上报 `Adapter:qd-social-security`；详见 [模拟站点与可执行示例](examples/README.md)。真实北京医保只提供结构草稿，须获得合法地址、页面选择器、登录授权、客户级隔离和端到端验收，参见 [北京医保操作说明](beijing-medical-query.md)。系统未默认发布真实政务工作流。

## 版本与当前限制

工作流每次保存创建新版本，发布后才能创建任务；既有任务固定其创建时版本。停用阻断新任务及未派发任务，在线执行受取消和下一步门禁约束。设计器尚无单步在线调试；调试需在获授权的模拟/测试系统中提交任务，结合任务详情检查点、日志、任务结果 JSON 和产物核对。真实站点适配、UKey/验证码厂商 Provider、组织租户隔离、浏览器可视人工接管、跨工作流引用与生产级回滚仍有未完成任务，详见 [开发计划](development-plan.md)。

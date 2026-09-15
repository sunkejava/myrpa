# RPA Workflow 设计

## 1. 设计目标

Workflow 是 AgentRPA 的确定性执行单元。业务语义由 Agent 负责，具体页面操作由 Workflow 描述，浏览器执行器只负责执行 Step。

## 2. 层次

`Function → Workflow → WorkflowVersion → Step`

一个业务功能可以拥有多个版本，但同一时间只有一个版本处于 Active。

## 3. Step 类型

建议第一阶段支持：

- Navigate：打开页面
- Click：点击元素
- Input：输入文本
- Select：选择下拉项
- Wait：等待
- WaitForElement：等待元素
- Extract：提取页面数据
- Upload：上传文件
- Download：下载文件
- Screenshot：截图
- Script：执行受限 JavaScript
- Condition：条件判断
- Loop：循环
- SubWorkflow：调用子流程
- HumanTask：人工介入

## 4. 元素定位

优先级：稳定业务属性 > data-testid > CSS > XPath > 文本。禁止依赖易变化的绝对 XPath 作为唯一定位方式。

每个元素可以配置多个 Locator，并支持 fallback。

## 5. Workflow 示例

```json
{
  "name": "社保缴费查询",
  "version": 2,
  "steps": [
    { "type": "Navigate", "url": "{{system.baseUrl}}" },
    { "type": "Login" },
    { "type": "Input", "target": "person.idCard", "value": "{{item.idCard}}" },
    { "type": "Select", "target": "period", "value": "{{task.period}}" },
    { "type": "Click", "target": "queryButton" },
    { "type": "Extract", "target": "paymentResult", "output": "item.result" }
  ]
}
```

实际系统中凭据、验证码等敏感内容不得写入 Workflow JSON。

## 6. 可复用组件

Workflow 应支持子流程，例如：

- 通用登录
- 页面初始化
- 验证码人工处理
- 通用分页
- 文件下载
- 结果校验
- 退出登录

避免每个业务流程复制一份完整登录流程。

## 7. 版本管理

WorkflowVersion 必须包含版本号、状态、发布时间、创建人、变更说明和测试结果。生产任务必须绑定实际执行版本，历史任务不能因新版本发布而改变执行记录。

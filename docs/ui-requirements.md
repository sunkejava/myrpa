# Vue 前端 UI 与组件化要求

## 1. 总体视觉

AgentRPA 后端管理系统采用现代科技感视觉，但不能做成廉价的“满屏霓虹灯”。整体定位为：**企业级 RPA 控制中心 + 机器人调度中心**。

视觉元素可以使用机器人、自动化流程节点、任务轨道、执行状态、浏览器窗口、连接线、运行脉冲等元素，重点体现“自动化、流程、机器人、实时执行”。

支持 Light / Dark，并允许用户自定义主题。

## 2. 用户自定义主题

用户可以配置：

- 明暗模式
- 主色
- 辅助色
- 页面背景
- 卡片风格
- 圆角
- 阴影强度
- 密度
- 动效开关
- 科技元素强度

主题必须基于 Design Token/CSS Variables 实现，禁止在业务页面大量写死颜色。

## 3. 多语言

前端不得把展示文本散落硬编码。所有界面文本通过 i18n key 获取。

支持：

- 简体中文
- English
- 用户自定义语言包

语言资源按模块拆分，例如 `common`、`task`、`workflow`、`system`、`permission`，便于维护和扩展。

## 4. Vue 组件化硬性要求

**禁止为了快速开发把所有代码堆在页面文件中。** 页面只负责组合业务组件、调用 Store/API 和处理页面级状态。

推荐结构：

```text
src/
├── api/
│   ├── modules/
│   └── http.ts
├── components/
│   ├── common/
│   ├── form/
│   ├── table/
│   ├── task/
│   ├── workflow/
│   ├── robot/
│   └── visualization/
├── composables/
├── layouts/
├── locales/
├── router/
├── stores/
├── styles/
│   ├── tokens.css
│   ├── theme.css
│   └── index.css
├── types/
└── views/
```

## 5. 通用查询组件

封装 `SearchForm`，统一处理：

- 查询条件
- 重置
- 展开/收起
- 字段布局
- 响应式
- 参数序列化
- loading
- 查询事件

页面只声明 schema，不重复编写相同表单布局。

## 6. 通用表格组件

封装 `DataTable`，统一处理：

- 列配置
- 分页
- 排序
- 筛选
- 多选
- 批量操作
- 加载状态
- 空数据
- 表格密度
- 列显示控制
- 导出
- 刷新
- 操作列

业务页面通过 column schema 配置列，而不是复制表格模板。

## 7. CRUD 页面模式

对于城市、业务系统、业务功能、角色、用户、凭据等标准管理页面，优先采用：

`SearchForm + DataTable + FormDialog + DetailDrawer + ConfirmAction`

这些组件应该可以被不同模块复用。

## 8. RPA 专用组件

提供：

- `RobotStatusCard`
- `TaskProgress`
- `TaskTimeline`
- `WorkflowNode`
- `WorkflowCanvas`
- `ExecutionLog`
- `HumanInterventionPanel`
- `BrowserPreview`
- `TaskItemTable`

例如任务中心应能直观看到：

`机器人 #03 → 北京社保 → 社保缴费查询 → 第 47/100 人 → 正在提取结果`

## 9. 页面职责

View 页面不得承担：

- 大量 API 封装
- 重复表格实现
- 重复表单实现
- 主题逻辑
- i18n 逻辑
- 复杂数据转换
- Workflow 引擎逻辑

这些能力分别进入 api、components、composables、stores、services。

## 10. 可复用优先原则

新增页面前必须先检查现有组件。如果两个以上页面存在相同 UI/交互，应优先抽取公共组件。

组件必须有明确 Props、Emits、类型定义和使用说明，避免形成“大而全”的万能组件。

## 11. 科技感与可用性平衡

禁止：

- 大量闪烁动画
- 影响阅读的发光效果
- 过度透明玻璃效果
- 为科技感牺牲表格可读性
- 每个页面使用完全不同的视觉风格

科技元素应服务于任务状态、机器人状态和流程可视化，而不是单纯装饰。

# 开发实施计划

## Phase 0：基础工程

- .NET 10 Solution
- DDD/Clean Architecture
- Vue 3 + TypeScript
- 统一 API Client
- 统一异常处理
- JWT/Identity
- 数据库迁移
- i18n
- Theme Design Token
- 基础 Layout
- 通用 SearchForm/DataTable/FormDialog

## Phase 1：平台基础

- 用户
- 角色
- 组织
- 城市
- 业务系统
- 业务功能
- 细粒度权限
- 审计

验收标准：可以完成“北京 → 社保系统 → 社保缴费查询 → 执行”粒度的权限授权。

## Phase 2：Workflow

- Workflow CRUD
- Workflow Version
- Step Schema
- 子流程
- Locator 管理
- 流程测试
- 发布/停用

## Phase 3：RPA Engine

- Browser Worker
- 浏览器生命周期
- Step Runner
- Retry Policy
- Screenshot
- Download/Upload
- Execution Log
- 人工接管

## Phase 4：Agent

- Chat
- Intent Parser
- Resource Resolver
- TaskPlan
- Permission Check
- Confirmation
- Workflow Selection

## Phase 5：批量业务

- Excel/CSV
- TaskItem
- 并发控制
- 失败重试
- 断点续跑
- 结果导出

## Phase 6：运营中心

- 机器人资源池
- Worker 状态
- 实时任务监控
- 执行统计
- 失败分析
- 审计查询

## Phase 7：扩展能力

- 更多城市
- 更多业务系统 Adapter
- 多 LLM Provider
- 本地模型
- 云模型
- 对象存储
- 分布式 Worker

## 开发质量要求

每个 Phase 必须保持可运行状态。业务页面优先复用公共组件；发现重复实现时先抽取组件再继续开发。所有 API、实体、服务和复杂前端逻辑都需要中文注释或文档说明，方便后续维护。

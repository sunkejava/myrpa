# API 设计规范

## 1. 总体原则

后端采用 .NET 10 + ASP.NET Core Web API，API 按业务模块组织，不允许 Controller 承担领域业务逻辑。

推荐：

`Controller → Application → Domain → Infrastructure`

## 2. 模块

建议 API 模块：

- Auth
- Users
- Roles
- Organizations
- Cities
- BusinessSystems
- BusinessFunctions
- Permissions
- Workflows
- WorkflowVersions
- Agents
- Tasks
- TaskItems
- Executions
- Credentials
- Files
- Audit
- Monitoring
- Themes
- Locales

## 3. 返回模型

统一使用 ApiResponse<T>，分页使用 PagedResult<T>。错误响应包含稳定 ErrorCode、用户可理解的 Message 和可选 Details。

## 4. API 与权限

每个执行型 API 必须在 Application 层进行授权校验；不能仅依赖前端隐藏按钮。

## 5. 查询

列表接口统一支持分页、排序、筛选。排序字段必须使用服务端白名单映射，禁止将客户端输入直接拼接 SQL。

## 6. 任务 API

核心接口方向：

- `POST /api/tasks/plan`：根据自然语言生成 TaskPlan
- `POST /api/tasks`：创建任务
- `GET /api/tasks`：任务列表
- `GET /api/tasks/{id}`：任务详情
- `POST /api/tasks/{id}/start`：启动
- `POST /api/tasks/{id}/cancel`：取消
- `POST /api/tasks/{id}/resume`：恢复人工介入任务
- `GET /api/tasks/{id}/executions`：执行记录

最终路径以实际模块实现为准。

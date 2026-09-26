# AgentRPA 发布包部署

GitHub Actions 的 **Release Packages** 工作流先运行后端测试，再为 `linux-x64` 和 `win-x64` 生成同结构的自包含压缩包：`api/`、`node-agent/`、`frontend/`。推送 `v` 开头的版本标签且测试、两种平台打包都成功后，压缩包会附到 GitHub Release。工作流也支持手动运行；提交此文档或工作流会自动打包供验收。当前默认数据存储为 SQLite，多个 API 实例和生产级高可用尚未验收。

## 启动 API

分别设置 `AgentRPA__Bootstrap__AdminPassword`（至少 10 位）、`AgentRPA__Jwt__SigningKey`（至少 32 位随机字符）和 `ConnectionStrings__AgentRPA`（SQLite 文件路径）。将 API 设为 `Production`，配置 `ASPNETCORE_URLS` 为实际监听地址，然后在 `api` 目录运行 Linux `./AgentRPA.Api` 或 Windows `AgentRPA.Api.exe`。首次启动自动执行数据库迁移并创建管理员。数据库与执行产物目录应放在有写权限的持久化磁盘；更新前备份数据库。不要把真实口令或签名密钥写进发布包。

## 启动浏览器节点

在节点所在主机安装 Chromium：`npx playwright@1.55.0 install chromium`（需要 Node.js，Linux 还需浏览器系统依赖）。通过 `NodeAgent__ServerUrl` 指向 API 地址，按节点注册流程配置 `NodeAgent__RegistrationKey`，在 `node-agent` 目录运行 `./AgentRPA.NodeAgent` 或 `AgentRPA.NodeAgent.exe`。节点必须能访问 API、SignalR `/hubs/node-agent` 和目标业务站点；证书须受信任。

## 托管前端

将 `frontend/` 作为静态网站目录，保留 Vue 路由的回退规则（未知前端路径返回 `index.html`）。在同一站点反向代理 `/api/` 与 `/hubs/node-agent` 到 API；WebSocket 代理需要允许 Upgrade。前端使用相对 API 地址，静态网站不包含任何密钥。发布包仅提供产物，TLS、反向代理、服务管理、数据库备份及外部系统凭据仍由部署环境配置。
## 远程执行产物网关

API 默认使用本地 `AgentRPA:Artifacts:Root` 存储；多实例部署可将 `AgentRPA__Artifacts__Provider=RemoteHttp`，并通过环境变量配置 `AgentRPA__Artifacts__Remote__Endpoint=https://<受控网关>/artifacts/` 与 `AgentRPA__Artifacts__Remote__BearerToken=<独立服务密钥>`。密钥不得写入仓库或工作流。网关需要以同一键提供 `PUT` 上传、`HEAD` 存在检查、`GET` 下载、`DELETE` 删除，成功返回 2xx、不存在返回 404，并自行持久化文件及限制服务端 Bearer 凭据访问。API 只请求配置的 HTTPS 地址、拒绝重定向和非法 StorageKey；上传在 API 内校验 SHA256 和长度后才登记。切换存储后既有本地产物不会自动搬迁，切换前应规划迁移或保留旧存储。此配置接入的是上述 HTTP 网关协议，不直接实现 S3/OSS API。

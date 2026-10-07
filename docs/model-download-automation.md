# Bing → ModelScope → GGUF 模型批量下载

## 默认链路

仓库启动时幂等初始化已发布的 **模型搜索与 GGUF 下载** 工作流，位于“北京市 / 公共模型资源 · ModelScope / 模型搜索与 GGUF 下载”。这里只借用现有城市资源层级管理公共资源，不表示模型下载限定北京。管理员自动获得此功能的 Execute、Download 权限；已有停用、拒绝授权及工作流修改不会被启动脚本覆盖。其他用户由管理员在权限中心分别授权。

用户提出的 modelspace 按魔搭 **ModelScope** 实现。默认搜索词保留 modelspace，并补充 ModelScope 魔搭以消除歧义。默认仓库为 `unsloth/Qwen3.8-27B-GGUF`，文件为 `Qwen3.8-27B-UD-Q4_K_M.gguf`。已核对模型文件页面：https://modelscope.cn/models/unsloth/Qwen3.8-27B-GGUF/files 。该文件页面标示约 16.46 GB，实际字节数和摘要以每次执行时的官方仓库元数据为准。

| 节点 | 操作 | 完成条件 |
| --- | --- | --- |
| bing / Navigate | 打开 https://www.bing.com/ | 页面加载 |
| site-search / Input | 填入站点搜索词 | 搜索框填写成功 |
| search-submit / Press | Enter 发起 Bing 搜索 | 浏览器进入结果页 |
| model-download / ModelDownload | 从 Bing 结果进入魔搭；操作站内模型搜索；精确匹配仓库；打开模型文件页；定位指定 GGUF | 文件存在于指定仓库与版本，完成流式下载、大小、GGUF 文件头和 SHA256 校验 |
| end / End | 完成执行 | 日志、视频、下载清单上传后才报告成功 |

浏览器搜索和定位全程录屏。大型文件通过 ModelScope 官方仓库下载接口流式传输，避免浏览器临时文件占用与 50 MiB 普通产物上传限制；下载发生在 NodeAgent，而非 API 或前端电脑。默认不是随机下载搜索结果中的第一个文件，也不会误选 mmproj 或 imatrix。ModelDownload 的页面定位超时由 `timeoutMs` 控制，权重传输与摘要校验另由 `downloadTimeoutSeconds` 控制。

## Windows 开发及运行

先按 [Windows 开发说明](windows-development.md) 启动 API 和前端。本地 API 可为 8666、前端为 5173；Vite `VITE_API_PROXY_TARGET=http://localhost:8666`，NodeAgent 的 ServerUrl 也指向 8666。账户密码沿用已有数据库；新开发数据库默认 admin / 123456。

在仓库根目录用 PowerShell：

```powershell
dotnet build AgentRPA.sln -c Release
pwsh src/backend/AgentRPA.NodeAgent/bin/Release/net10.0/playwright.ps1 install chromium

# RegistrationKey 必须与 API 的 NodeAuthentication:RegistrationKey 一致。
$env:NodeAgent__ServerUrl = 'http://localhost:8666'
$env:NodeAgent__RegistrationKey = '替换成你的节点注册密钥'
$env:NodeAgent__AgentKey = 'windows-model-download-01'
$env:NodeAgent__Name = 'Windows 模型下载节点'
$env:NodeAgent__ModelDownloads__DownloadRoot = 'D:\RPA\Models'
$env:NodeAgent__ModelDownloads__MaxFileBytes = '107374182400'
dotnet run --project src/backend/AgentRPA.NodeAgent -c Release --no-build
```

在“节点管理”批准新注册节点，确认在线且具备 `Browser:Edge`、`ModelDownload:ModelScope` 能力。执行引擎实际使用 Playwright Chromium；Browser:Edge 是本项目已有的浏览器能力标识。节点每 15 秒对活动执行续租，长时间下载及校验不会因没有页面点击而过期。

下载盘必须容纳完整权重并保留至少 256 MiB 余量。默认单文件上限 100 GiB，可通过 MaxFileBytes 调整。API 只保存下载清单、日志和视频，不会将十几 GB 权重复制到 API。反向代理需允许视频上传至 512 MiB，普通产物上限仍为 50 MiB；长录像可在节点本地留存，超限会明确报告上传失败。

## 前端单模型与 Excel 批量操作

1. 打开“Workflow 管理”，在“已有 Workflow”选择“模型搜索与 GGUF 下载”，查看 v1 已发布版本。
2. 单模型使用“填入流程默认数据”。批量使用“下载模型 Excel 模板”，在 Excel 中编辑后从“导入 Excel、JSON 数组或 UTF-8 CSV”上传。
3. 第一张表的第一行必须保持参数名，第二行起每行一个目标文件；删除不需要的样例行。不要插入说明标题行、合并单元格或公式。只读取第一张工作表，单批 1–100 行、上传文件不超过 1 MB。模板全部单元格按文本保存。
4. 导入后检查 JSON 预览，点击“校验草稿数据”，再选择“实际执行版本” v1，点击“创建并运行测试任务”。实际执行会再次校验该已发布版本的参数和 Execute / Download 权限。校验草稿本身不操作外部网站。
5. 在“任务中心”查看任务和每行执行状态；“查看详情”→选择执行实例，查看日志、下载清单和视频。

| Excel 列名 | 含义 | 默认值 / 约束 |
| --- | --- | --- |
| siteQuery | Bing 站点搜索词 | modelspace ModelScope 魔搭 |
| modelQuery | 魔搭站内搜索关键词 | qwen3.8 27B |
| repository | 精确仓库 owner/name | unsloth/Qwen3.8-27B-GGUF |
| fileName | 精确 GGUF 路径（可带仓库内子目录） | Qwen3.8-27B-UD-Q4_K_M.gguf |
| revision | 官方仓库版本 | master；建议固定实际已存在版本以便复现 |
| downloadTimeoutSeconds | 下载与校验总超时，文本秒数 | 21600；允许 60–86400 |

其他模型必须同时修改 modelQuery、repository 和 fileName；先在魔搭确认仓库与文件存在。多分片模型每个分片填写一行；本流程下载各分片，不执行模型合并或推理。每个任务行有独立录像和日志。重复目标文件通过大小和摘要校验后复用，清单 `Reused=true`。

## 文件、日志和视频在哪里

权重路径：`DownloadRoot\owner\repository\revision\fileName`。例如 `D:\RPA\Models\unsloth\Qwen3.8-27B-GGUF\master\Qwen3.8-27B-UD-Q4_K_M.gguf`。

每次执行节点还保留独立 `artifacts/runs/<随机执行目录>/execution.jsonl` 和 `videos/*.webm`。路径相对启动 NodeAgent 的工作目录；用户可在任务日志查看模型完整路径。任务详情的产物提供：

| 类型 | 内容与使用方式 |
| --- | --- |
| ExecutionLog | 完整 JSONL：UTC 时间、开始/完成节点、阶段进度、失败原因、脱敏页面路径；下载后逐行分析 |
| Video | 1280×720 浏览器 WebM；点击“播放视频”或“下载”；覆盖从打开 Bing 到结束或失败 |
| DownloadManifest | JSON：仓库、版本、文件、节点绝对路径、字节数、SHA256、是否复用 |
| Screenshot | 失败现场截图；浏览器崩溃无法截图时仍尝试保存已有日志和录像 |

服务端按任务所有人鉴权读取日志及产物；日志不写入参数值或提取结果，页面 URL 去掉查询参数，错误消息沿用脱敏规则。录像直接反映公开模型页面，可能包含页面上显示的信息；通用工作流可通过根字段 `recordVideo: true` 开启录像，默认模型模板已开启。

## 中断恢复与排错

- Bing 未返回正确魔搭结果或触发验证码：执行失败并保存证据；调整 siteQuery 或网络后重新运行。不得把其他同名站点当作魔搭。
- 模型卡片未找到：确认模型已公开、搜索词能检索到 repository。页面结构变化时更新 ModelDownloadStep 中的定位，不绕过仓库校验。
- 文件不存在：按指定 revision 的官方元数据报错，不静默切换另一文件或量化精度。
- 下载中断：节点保留 `.partial` 与 `.partial.sha256`。再次运行完全相同的仓库、版本、路径会按 Range 恢复；远端不支持 Range 则安全从头写入。
- 远端内容更新：SHA256 不同会拒绝拼接旧断点。移走旧 `.partial` 和 `.sha256` 后重试。摘要或 GGUF 头校验失败的完整断点也需移走再重试。
- 已有最终文件摘要或大小不符：不覆盖用户数据；移走旧文件后重试。
- 无录像：确认流程根字段 recordVideo=true、已安装 Chromium、API/反向代理上传限制足够；日志和录像在 Context 关闭后上传，运行中尚无完整录像是正常情况。
- NodeAgent 被强杀、断电或浏览器根本无法启动时不能保证最后的视频帧或服务端上传，检查 NodeAgent 控制台及本地目录。正常可捕获的流程失败会保存证据。

## 自动化回归

单元测试 `ModelFileDownloaderTests` 验证内容校验、断点续传、忽略 Range 的重启、版本变化、损坏数据、文件缺失、大小限制及路径安全。端到端测试 `model-download.spec.ts` 启动真实 .NET NodeAgent、真实浏览器和本机 Bing/魔搭契约测试站点：从仓库提供的 XLSX 模板导入、发布版本、批量执行两行、下载权重、复用、读取成功/失败录像与日志，最后在前端解码 WebM。

```powershell
dotnet test tests/AgentRPA.Tests -c Release --filter FullyQualifiedName~ModelFileDownloaderTests
# 完整前端/真实节点集成由 GitHub Actions Frontend Build 按仓库配置启动 API 后执行：
cd src/frontend
npm run build
npm run test:e2e -- e2e/model-download.spec.ts --project=desktop-chromium
```

CI 使用小型 GGUF 测试文件，不冒充真实 27B 权重，不依赖第三方搜索排序，也不下载十几 GB 文件。实际外网验收按上述前端步骤运行默认任务，确认清单字节数和 SHA256 与官方元数据一致。

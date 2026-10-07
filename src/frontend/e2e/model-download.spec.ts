import { expect, test } from '@playwright/test'
import { spawn } from 'node:child_process'
import { createServer } from 'node:http'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { resolve } from 'node:path'
import { createHash, randomUUID } from 'node:crypto'

test('Excel 模型批量下载经过浏览器搜索并保留成功和失败的日志视频', async ({ page, request, isMobile }, testInfo) => {
  test.skip(isMobile, '共享节点集成测试仅在桌面运行一次。')
  test.setTimeout(210000)
  const api = 'http://127.0.0.1:5000'
  const model = Buffer.from('GGUF-browser-integration-weight-fixture')
  const sha256 = createHash('sha256').update(model).digest('hex')
  const repository = 'unsloth/Qwen3.8-27B-GGUF'
  const filename = 'Qwen3.8-27B-UD-Q4_K_M.gguf'
  const secondModel = { repository: 'fixture/Second-7B-GGUF', filename: 'second-model.gguf', query: 'fixture second model', content: Buffer.from('GGUF-second-browser-model-fixture') }
  const catalogModels = [{ repository, filename, query: 'qwen3.8 27B', content: model }, secondModel]
  let transfers = 0
  const visited: string[] = []
  const server = createServer((req, res) => {
    const url = new URL(req.url!, 'http://localhost'); visited.push(url.pathname)
    const target = catalogModels.find(x => url.pathname.includes(`/models/${x.repository}`))
    if (url.pathname.endsWith('/repo/files')) {
      res.setHeader('Content-Type', 'application/json')
      res.end(JSON.stringify({ Data: { Files: target ? [{ Path: target.filename, Type: 'file', Size: target.content.length, Sha256: createHash('sha256').update(target.content).digest('hex') }] : [] } })); return
    }
    if (url.pathname.endsWith('/repo') && target) { transfers++; res.setHeader('Content-Length', target.content.length); res.end(target.content); return }
    res.setHeader('Content-Type', 'text/html; charset=utf-8')
    if (url.pathname === '/bing') res.end('<form action="/bing-search"><input id="sb_form_q" name="q"></form>')
    else if (url.pathname === '/bing-search') res.end('<ol id="b_results"><li class="b_algo"><h2><a href="/">魔搭 ModelScope</a></h2></li></ol>')
    else if (url.pathname === '/models') {
      const match = catalogModels.find(x => x.query === url.searchParams.get('name'))
      res.end(`<form><input name="name" placeholder="输入关键词 搜索您想要的模型"></form>${match ? `<a href="/models/${match.repository}">模型卡片</a>` : ''}`)
    }
    else if (target && url.pathname === `/models/${target.repository}`) res.end(`<button role="tab" onclick="document.getElementById('files').hidden=false">模型文件</button><div id="files" hidden><a href="/models/${target.repository}/file/view/master/${target.filename}">${target.filename} GGUF</a><a href="#">missing.gguf GGUF</a></div>`)
    else res.end('<h1>ModelScope fixture</h1>')
  })
  await new Promise<void>(done => server.listen(0, '127.0.0.1', done))
  const address = server.address() as { port: number }
  const catalog = `http://127.0.0.1:${address.port}`
  const root = mkdtempSync(resolve(tmpdir(), 'rpa-model-'))
  let child: ReturnType<typeof spawn> | undefined
  let output = ''
  try {
    const login = await request.post(`${api}/api/auth/login`, { data: { userName: 'admin', password: 'BrowserTestPassword123!' } })
    expect(login.ok()).toBeTruthy()
    const token = (await login.json()).accessToken as string
    const headers = { Authorization: `Bearer ${token}` }
    const post = async (path: string, data: unknown) => {
      const result = await request.post(`${api}/api/${path}`, { data, headers }); const body = await result.text()
      expect(result.ok(), `${path}: ${body}\n${output}`).toBeTruthy(); return body ? JSON.parse(body) : undefined
    }
    const workflows = await (await request.get(`${api}/api/workflows`, { headers })).json() as Array<{ id: string; name: string; businessFunctionId: string }>
    const seeded = workflows.find(x => x.name === '模型搜索与 GGUF 下载')!
    expect(seeded).toBeDefined()
    const unique = `ModelFixture:${randomUUID()}`
    const definition = JSON.parse(readFileSync(resolve(process.cwd(), '../../docs/examples/modelscope-gguf-download.json'), 'utf8'))
    expect(definition).toEqual(JSON.parse(readFileSync(resolve(process.cwd(), 'src/data/workflow-templates/modelscope-gguf-download.json'), 'utf8')))
    definition.steps[0].config.url = catalog + '/bing'; definition.executionRequirement.requiredCapabilities.push(unique)
    const workflow = await post('workflows', { businessFunctionId: seeded.businessFunctionId, name: `模型下载验收-${unique}` })
    const version = await post(`workflows/${workflow.id}/versions`, { definitionJson: JSON.stringify(definition) })
    await post(`workflows/${workflow.id}/versions/${version.version}/publish`, {})
    const workbook = await request.get(`${api}/api/model-downloads/template`, { headers })
    expect(workbook.ok()).toBeTruthy()
    expect(await workbook.body()).toEqual(readFileSync(resolve(process.cwd(), '../../docs/examples/model-download-template.xlsx')))
    const imported = await request.post(`${api}/api/workflows/${workflow.id}/test-data/import`, { headers, multipart: { file: { name: 'models.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: await workbook.body() } } })
    expect(imported.ok(), await imported.text()).toBeTruthy()
    const rows = (await imported.json()).rows as Array<Record<string, string>>
    expect(rows).toEqual([expect.objectContaining({ repository, fileName: filename, modelQuery: 'qwen3.8 27B' })])
    expect(await post(`workflows/${workflow.id}/test-data/validate`, { definitionJson: JSON.stringify(definition), items: rows.map(x => JSON.stringify(x)) })).toMatchObject({ valid: true })
    const agentKey = `model-${randomUUID()}`
    const registrationKey = 'BrowserNodeRegistrationKey123!'
    const registration = await request.post(`${api}/api/nodes/register`, { headers: { 'X-Node-Registration-Key': registrationKey }, data: { agentKey, name: '模型下载测试节点', nodeKind: 'Physical', osPlatform: 'Linux', architecture: 'X64', agentVersion: '0.1.0', networkZone: 'default', capabilities: [{ code: 'Browser:Edge' }, { code: 'ModelDownload:ModelScope' }, { code: unique }], workerSlots: ['worker-01'] } })
    expect(registration.ok()).toBeTruthy(); const node = await registration.json()
    await post(`nodes/${node.nodeId}/approve`, {})
    child = spawn('dotnet', [resolve(process.cwd(), '../backend/AgentRPA.NodeAgent/bin/Release/net10.0/AgentRPA.NodeAgent.dll')], { env: { ...process.env, NodeAgent__ServerUrl: api, NodeAgent__AgentKey: agentKey, NodeAgent__RegistrationKey: registrationKey, NodeAgent__Name: '模型下载测试节点', NodeAgent__OsPlatform: 'Linux', NodeAgent__Capabilities__0__Code: 'Browser:Edge', NodeAgent__Capabilities__1__Code: unique, NodeAgent__Capabilities__2__Code: 'ModelDownload:ModelScope', NodeAgent__ModelDownloads__CatalogBaseUrl: catalog, NodeAgent__ModelDownloads__DownloadRoot: root } })
    child.stdout?.on('data', x => { output += x.toString() }); child.stderr?.on('data', x => { output += x.toString() })
    const run = async (items: Record<string, string>[], status: string) => {
      const name = `GGUF-${randomUUID()}`
      const task = await post('tasks', { workflowId: workflow.id, workflowVersion: version.version, name, items: items.map(x => JSON.stringify(x)), maxRetries: 0 })
      expect(task.approvalRequired).toBe(false); await post(`tasks/${task.id}/queue`, {})
      let detail: any
      await expect.poll(async () => { detail = await (await request.get(`${api}/api/tasks/${task.id}`, { headers })).json(); return detail.status }, { timeout: 85000, message: `NodeAgent: ${output}` }).toBe(status)
      return { task: { ...task, name }, detail }
    }
    const secondRow = { ...rows[0], modelQuery: secondModel.query, repository: secondModel.repository, fileName: secondModel.filename }
    const successful = await run([rows[0], secondRow, rows[0]], 'Succeeded')
    expect(transfers).toBe(2) // 两个不同模型分别下载；第三行复用已校验文件。
    expect(readFileSync(resolve(root, repository, 'master', filename))).toEqual(model)
    expect(readFileSync(resolve(root, secondModel.repository, 'master', secondModel.filename))).toEqual(secondModel.content)
    expect(visited).toEqual(expect.arrayContaining(['/bing', '/bing-search', '/models', `/models/${repository}`]))
    const firstExecution = successful.detail.items[0].executions[0].id as string
    const evidence = async (executionId: string, expectedStatus: string) => {
      const artifacts = await (await request.get(`${api}/api/executions/${executionId}/artifacts`, { headers })).json() as Array<{ id: string; artifactType: string }>
      for (const type of ['Video', 'ExecutionLog']) {
        const artifact = artifacts.find(x => x.artifactType === type)!
        expect(artifact, type).toBeDefined()
        const content = await request.get(`${api}/api/executions/${executionId}/artifacts/${artifact.id}/content`, { headers })
        expect(content.ok()).toBeTruthy()
        await testInfo.attach(`model-${expectedStatus}-${type}`, { body: await content.body(), contentType: type === 'Video' ? 'video/webm' : 'application/x-ndjson' })
        if (type === 'Video') expect((await content.body()).subarray(0, 4)).toEqual(Buffer.from([0x1a, 0x45, 0xdf, 0xa3]))
        else { const events = (await content.text()).trim().split('\n').map(x => JSON.parse(x)); expect(events).toEqual(expect.arrayContaining([expect.objectContaining({ StepType: 'Press' }), expect.objectContaining({ StepType: 'ModelDownload' }), expect.objectContaining({ Status: expectedStatus })])) }
      }
      return artifacts
    }
    const artifacts = await evidence(firstExecution, 'Succeeded')
    for (const item of successful.detail.items.slice(1)) await evidence(item.executions[0].id, 'Succeeded')
    const manifest = artifacts.find(x => x.artifactType === 'DownloadManifest')!
    expect(manifest).toBeDefined()
    const receipt = await (await request.get(`${api}/api/executions/${firstExecution}/artifacts/${manifest.id}/content`, { headers })).json()
    expect(receipt).toMatchObject({ Sha256: sha256, Bytes: model.length, Reused: false })
    const logs = await (await request.get(`${api}/api/executions/${firstExecution}/logs?limit=1000`, { headers })).json()
    expect(logs).toEqual(expect.arrayContaining([expect.objectContaining({ message: expect.stringContaining('下载完成') })]))
    const failed = await run([{ ...rows[0], fileName: 'missing.gguf' }], 'Failed')
    expect(failed.detail.items[0].executions[0].error).toContain('未找到唯一匹配')
    expect(await evidence(failed.detail.items[0].executions[0].id, 'Failed')).toEqual(expect.arrayContaining([expect.objectContaining({ artifactType: 'Screenshot' })]))
    // 在真实前端页面校验 Excel 导入预览和视频解码，而不只检查 API 的文件存在。
    await page.goto('/')
    await page.getByLabel('用户名').fill('admin'); await page.getByLabel('密码').fill('BrowserTestPassword123!'); await page.getByRole('button', { name: '登录', exact: true }).click()
    await page.getByRole('button', { name: 'Workflow 管理', exact: true }).click()
    await page.getByLabel('已有 Workflow').selectOption(workflow.id)
    await page.locator('.workflow-test-panel input[type=file]').setInputFiles({ name: 'models.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: await workbook.body() })
    await expect(page.locator('.workflow-test-panel textarea')).toHaveValue(JSON.stringify(rows, null, 2))
    await page.getByRole('button', { name: '校验草稿数据', exact: true }).click()
    await expect(page.locator('.test-report')).toContainText('静态校验通过')
    await page.getByRole('button', { name: '任务中心', exact: true }).click()
    const taskRow = page.locator('tr').filter({ hasText: successful.task.name })
    await taskRow.getByRole('button', { name: '查看详情', exact: true }).click()
    await page.getByRole('button', { name: new RegExp(firstExecution.slice(0, 8)) }).click()
    await page.getByRole('button', { name: '播放视频', exact: true }).click()
    await expect(page.locator('video')).toBeVisible()
    await expect.poll(() => page.locator('video').evaluate((element: HTMLVideoElement) => element.readyState)).toBeGreaterThanOrEqual(1)
    await post(`nodes/${node.nodeId}/drain`, {})
  } finally {
    if (child) { child.kill('SIGTERM'); await new Promise<void>(done => { child!.once('exit', () => done()); setTimeout(() => { child!.kill('SIGKILL'); done() }, 3000) }) }
    await new Promise<void>(done => server.close(() => done())); rmSync(root, { recursive: true, force: true })
  }
})

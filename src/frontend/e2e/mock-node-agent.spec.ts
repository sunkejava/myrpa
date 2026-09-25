import { expect, test } from '@playwright/test'
import { spawn } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const api = 'http://127.0.0.1:5000'

test('审批后的增减员任务由真实 NodeAgent 连续执行并记录提交检查点', async ({ request, isMobile }) => {
  test.skip(isMobile, '该测试操作共享的 NodeAgent；桌面项目执行一次。')
  test.setTimeout(180_000)
  const login = async (userName: string, password: string) => {
    const response = await request.post(`${api}/api/auth/login`, { data: { userName, password } })
    expect(response.ok()).toBeTruthy()
    return { Authorization: `Bearer ${(await response.json()).accessToken as string}` }
  }
  const admin = await login('admin', 'BrowserTestPassword123!')
  const post = async (path: string, data: unknown, headers = admin) => {
    const response = await request.post(`${api}${path}`, { data, headers })
    const body = await response.text()
    expect(response.ok(), `${path}: ${response.status()} ${body}`).toBeTruthy()
    return body ? JSON.parse(body) : undefined
  }
  const city = await post('/api/business-resources/cities', { code: `MOCK-${Date.now()}`, name: '模拟社保城市' })
  const system = await post(`/api/business-resources/cities/${city.id}/systems`, { code: 'SOCIAL', name: '模拟社保' })
  const businessFunction = await post(`/api/business-resources/systems/${system.id}/functions`, { code: 'ADD', name: '增员' })
  const removeFunction = await post(`/api/business-resources/systems/${system.id}/functions`, { code: 'REMOVE', name: '减员' })
  const operatorName = `mock${Date.now()}`
  const operator = await post('/api/user-management/users', { userName: operatorName, displayName: '模拟操作员', password: 'MockTestPassword123!' })
  await post('/api/permission-policies', { subjectId: operator.id, cityId: city.id, systemId: system.id, functionId: businessFunction.id, action: 'Execute' })
  await post('/api/permission-policies', { subjectId: operator.id, cityId: city.id, systemId: system.id, functionId: removeFunction.id, action: 'Execute' })
  const definition = readFileSync(resolve(process.cwd(), '../../docs/examples/qd-social-security-add.json'), 'utf8')
  const workflow = await post('/api/workflows', { businessFunctionId: businessFunction.id, name: '青岛模拟增员', description: '真实 NodeAgent 集成验收' })
  const version = await post(`/api/workflows/${workflow.id}/versions`, { definitionJson: definition })
  await post(`/api/workflows/${workflow.id}/versions/${version.version}/publish`, {})
  const removeDefinition = readFileSync(resolve(process.cwd(), '../../docs/examples/qd-social-security-remove.json'), 'utf8')
  const removeWorkflow = await post('/api/workflows', { businessFunctionId: removeFunction.id, name: '青岛模拟减员', description: '真实 NodeAgent 集成验收' })
  const removeVersion = await post(`/api/workflows/${removeWorkflow.id}/versions`, { definitionJson: removeDefinition })
  await post(`/api/workflows/${removeWorkflow.id}/versions/${removeVersion.version}/publish`, {})

  const operatorHeaders = await login(operatorName, 'MockTestPassword123!')
  const missingParameters = await request.post(`${api}/api/tasks`, { headers: operatorHeaders,
    data: { workflowId: workflow.id, workflowVersion: version.version, name: '参数缺失', items: ['{}'] } })
  expect(missingParameters.status()).toBe(400)
  expect(await missingParameters.text()).toContain('mockBaseUrl')
  const task = await post('/api/tasks', { workflowId: workflow.id, workflowVersion: version.version,
    name: '模拟增员', maxRetries: 0, items: [JSON.stringify({ mockBaseUrl: api, employeeName: '真实节点测试', idNumber: '110105194912310038' })] }, operatorHeaders)
  expect(task.approvalRequired).toBe(true)
  const approvals = await (await request.get(`${api}/api/task-approvals`, { headers: admin })).json() as Array<{ id: string, taskId: string }>
  const approval = approvals.find(item => item.taskId === task.id)
  expect(approval).toBeDefined()
  const premature = await request.post(`${api}/api/tasks/${task.id}/queue`, { data: {}, headers: operatorHeaders })
  expect(premature.status()).toBe(428)
  await post(`/api/task-approvals/${approval!.id}/decide`, { approved: true })

  const registrationKey = 'BrowserNodeRegistrationKey123!'
  const agentKey = `browser-agent-${Date.now()}`
  const registration = await request.post(`${api}/api/nodes/register`, {
    headers: { 'X-Node-Registration-Key': registrationKey }, data: {
      agentKey, name: 'Browser E2E Node', nodeKind: 'Physical', osPlatform: 'Linux', architecture: 'X64',
      agentVersion: '0.1.0', networkZone: 'default', capabilities: [{ code: 'Browser:Edge' }, { code: 'Adapter:qd-social-security' }], workerSlots: ['worker-01']
    }
  })
  expect(registration.ok(), await registration.text()).toBeTruthy()
  const node = await registration.json() as { nodeId: string }
  await post(`/api/nodes/${node.nodeId}/approve`, {})

  const dll = resolve(process.cwd(), '../backend/AgentRPA.NodeAgent/bin/Release/net10.0/AgentRPA.NodeAgent.dll')
  const child = spawn('dotnet', [dll], { env: { ...process.env, NodeAgent__ServerUrl: api,
    NodeAgent__RegistrationKey: registrationKey, NodeAgent__AgentKey: agentKey, NodeAgent__Name: 'Browser E2E Node',
    NodeAgent__OsPlatform: 'Linux', NodeAgent__Capabilities__0__Code: 'Browser:Edge' } })
  let output = ''
  child.stdout.on('data', data => { output += data.toString() })
  child.stderr.on('data', data => { output += data.toString() })
  try {
    await post(`/api/tasks/${task.id}/queue`, {}, operatorHeaders)
    const verifyTask = async (taskId: string) => {
      let detail: { status: string, items: Array<{ status: string, executions: Array<{ id: string, status: string, error: string }> }> } | undefined
      for (let attempt = 0; attempt < 85; attempt++) {
        const response = await request.get(`${api}/api/tasks/${taskId}`, { headers: operatorHeaders })
        expect(response.ok()).toBeTruthy()
        detail = await response.json()
        if (detail?.status === 'Succeeded' || detail?.status === 'Failed') break
        await new Promise(done => setTimeout(done, 1000))
      }
      expect(detail?.status, `任务状态: ${JSON.stringify(detail)}\nNodeAgent 日志:\n${output}`).toBe('Succeeded')
      expect(detail?.items[0].executions[0].status).toBe('Succeeded')
      const checkpointsResponse = await request.get(`${api}/api/executions/${detail!.items[0].executions[0].id}/checkpoints`, { headers: operatorHeaders })
      expect(checkpointsResponse.ok()).toBeTruthy()
      const checkpoints = await checkpointsResponse.json() as Array<{ stepId: string, eventType: string }>
      expect(checkpoints).toEqual(expect.arrayContaining([
        expect.objectContaining({ stepId: 'submit', eventType: 'StepStarted' }),
        expect.objectContaining({ stepId: 'submit', eventType: 'StepCompleted' }),
        expect.objectContaining({ stepId: 'result', eventType: 'StepCompleted' })
      ]))
    }
    await verifyTask(task.id)
    const mockSession = await request.post(`${api}/mock/qd-social-security/login`, { form: { username: 'demo', password: 'Demo123!' } })
    expect(mockSession.ok()).toBeTruthy()
    const employeeStatusUrl = `${api}/mock/qd-social-security/employees/status?idNumber=110105194912310038`
    const addedStatus = await request.get(employeeStatusUrl)
    expect(addedStatus.ok()).toBeTruthy()
    expect(await addedStatus.json()).toMatchObject({ active: true, employeeName: '真实节点测试' })
    const removeTask = await post('/api/tasks', { workflowId: removeWorkflow.id, workflowVersion: removeVersion.version,
      name: '模拟减员', maxRetries: 0, items: [JSON.stringify({ mockBaseUrl: api, employeeName: '真实节点测试', idNumber: '110105194912310038' })] }, operatorHeaders)
    expect(removeTask.approvalRequired).toBe(true)
    const removeApprovals = await (await request.get(`${api}/api/task-approvals`, { headers: admin })).json() as Array<{ id: string, taskId: string }>
    const removeApproval = removeApprovals.find(item => item.taskId === removeTask.id)
    expect(removeApproval).toBeDefined()
    await post(`/api/task-approvals/${removeApproval!.id}/decide`, { approved: true })
    await post(`/api/tasks/${removeTask.id}/queue`, {}, operatorHeaders)
    await verifyTask(removeTask.id)
    const removedStatus = await request.get(employeeStatusUrl)
    expect(removedStatus.ok()).toBeTruthy()
    expect(await removedStatus.json()).toMatchObject({ active: false, employeeName: null })
  } finally {
    child.kill('SIGTERM')
  }
})

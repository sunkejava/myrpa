import { expect, test } from '@playwright/test'
import { spawn, spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { randomUUID } from 'node:crypto'

const api = 'http://127.0.0.1:5000'

test('审批后的增减员任务由真实 NodeAgent 连续执行并记录提交检查点', async ({ page, request, isMobile }) => {
  test.skip(isMobile, '该测试操作共享的 NodeAgent；桌面项目执行一次。')
  test.setTimeout(210_000)
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
  const person = () => JSON.stringify({ mockBaseUrl: api, employeeName: '真实节点测试', idNumber: '110105194912310038', submissionId: randomUUID() })
  const task = await post('/api/tasks', { workflowId: workflow.id, workflowVersion: version.version,
    name: '模拟增员', maxRetries: 0, items: [person()] }, operatorHeaders)
  expect(task.approvalRequired).toBe(true)
  const approvals = await (await request.get(`${api}/api/task-approvals`, { headers: admin })).json() as Array<{ id: string, taskId: string }>
  const approval = approvals.find(item => item.taskId === task.id)
  expect(approval).toBeDefined()
  const premature = await request.post(`${api}/api/tasks/${task.id}/queue`, { data: {}, headers: operatorHeaders })
  expect(premature.status()).toBe(428)
  await post(`/api/task-approvals/${approval!.id}/decide`, { approved: true })
  const cancelledBeforeDispatch = await post('/api/tasks', { workflowId: workflow.id, workflowVersion: version.version,
    name: '入队前取消', maxRetries: 0, items: [person()] }, operatorHeaders)
  const unauthorizedCancel = await request.post(`${api}/api/tasks/${cancelledBeforeDispatch.id}/cancel`, { data: {}, headers: admin })
  expect(unauthorizedCancel.status()).toBe(404)
  const cancelled = await post(`/api/tasks/${cancelledBeforeDispatch.id}/cancel`, {}, operatorHeaders)
  expect(cancelled).toMatchObject({ status: 'Cancelled', activeExecutions: 0 })
  const queuedAfterCancel = await request.post(`${api}/api/tasks/${cancelledBeforeDispatch.id}/queue`, { data: {}, headers: operatorHeaders })
  expect(queuedAfterCancel.status()).toBe(409)

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
    // A repeat submission fails in the browser. The independent administrator confirms the
    // existing employee in the mock site and closes it without sending another browser command.
    const repeatedTask = await post('/api/tasks', { workflowId: workflow.id, workflowVersion: version.version,
      name: '重复增员核验', maxRetries: 0, items: [person()] }, operatorHeaders)
    const repeatedApprovals = await (await request.get(`${api}/api/task-approvals`, { headers: admin })).json() as Array<{ id: string, taskId: string }>
    const repeatedApproval = repeatedApprovals.find(item => item.taskId === repeatedTask.id)
    expect(repeatedApproval).toBeDefined()
    await post(`/api/task-approvals/${repeatedApproval!.id}/decide`, { approved: true })
    await post(`/api/tasks/${repeatedTask.id}/queue`, {}, operatorHeaders)
    let failedDetail: { status: string, items: Array<{ executions: Array<{ id: string }> }> } | undefined
    for (let attempt = 0; attempt < 75; attempt++) {
      failedDetail = await (await request.get(`${api}/api/tasks/${repeatedTask.id}`, { headers: operatorHeaders })).json()
      if (failedDetail?.status === 'Failed') break
      await new Promise(done => setTimeout(done, 1000))
    }
    expect(failedDetail?.status, `NodeAgent 日志:\n${output}`).toBe('Failed')
    const failedExecutionId = failedDetail!.items[0].executions[0].id
    const pendingReconciliations = await (await request.get(`${api}/api/task-reconciliations`, { headers: admin })).json() as Array<{ executionId: string }>
    expect(pendingReconciliations.some(entry => entry.executionId === failedExecutionId)).toBe(true)
    const unknownEvidence = await request.get(`${api}/api/task-reconciliations/${failedExecutionId}/mock-evidence`, { headers: admin })
    expect(unknownEvidence.ok()).toBeTruthy()
    expect(await unknownEvidence.json()).toMatchObject({ result: 'Inconclusive', receiptFound: false })
    const forbiddenEvidence = await request.get(`${api}/api/task-reconciliations/${failedExecutionId}/mock-evidence`, { headers: operatorHeaders })
    expect(forbiddenEvidence.status()).toBe(403)
    const selfReconcile = await request.post(`${api}/api/task-reconciliations/${failedExecutionId}/decide`, {
      headers: operatorHeaders, data: { decision: 'Submitted', evidenceReference: 'MOCK-STATUS-001' }
    })
    expect(selfReconcile.status()).toBe(403)
    await page.goto('/')
    await page.getByLabel('用户名').fill('admin')
    await page.getByLabel('密码').fill('BrowserTestPassword123!')
    await page.getByRole('button', { name: '登录', exact: true }).click()
    await page.getByRole('button', { name: '核验中心' }).click()
    await expect(page.getByText(repeatedTask.id)).toBeVisible()
    await page.getByLabel(`外部核验凭据 ${repeatedTask.id}`).fill('MOCK-STATUS-001')
    await page.getByLabel(`核验结论 ${repeatedTask.id}`).selectOption('Submitted')
    await page.locator('tr').filter({ hasText: repeatedTask.id }).getByRole('button', { name: '确认核验' }).click()
    await expect(page.getByRole('status')).toContainText('核验结论已记录')
    const reconciled = await (await request.get(`${api}/api/tasks/${repeatedTask.id}`, { headers: operatorHeaders })).json() as { status: string }
    expect(reconciled.status).toBe('Succeeded')
    const secondDecision = await request.post(`${api}/api/task-reconciliations/${failedExecutionId}/decide`, {
      headers: admin, data: { decision: 'Submitted', evidenceReference: 'MOCK-STATUS-001' }
    })
    expect(secondDecision.status()).toBe(409)
    const reconciledLogs = await (await request.get(`${api}/api/executions/${failedExecutionId}/logs`, { headers: operatorHeaders })).json() as Array<{ eventType: string }>
    expect(reconciledLogs.some(entry => entry.eventType === 'ManualReconciliation')).toBe(true)
    const removeTask = await post('/api/tasks', { workflowId: removeWorkflow.id, workflowVersion: removeVersion.version,
      name: '模拟减员', maxRetries: 0, items: [person()] }, operatorHeaders)
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

    // A successful external submission followed by a failing assertion has a matching receipt.
    // Only the administrator may use it as evidence; the lookup never mutates the task.
    const assertionFailure = JSON.parse(definition) as { steps: Array<{ id: string, config: { contains?: string } }> }
    assertionFailure.steps.find(step => step.id === 'result')!.config.contains = '不会出现的结果'
    const receiptWorkflow = await post('/api/workflows', { businessFunctionId: businessFunction.id, name: '提交后断言失败', description: '只读回执核验' })
    const receiptVersion = await post(`/api/workflows/${receiptWorkflow.id}/versions`, { definitionJson: JSON.stringify(assertionFailure) })
    await post(`/api/workflows/${receiptWorkflow.id}/versions/${receiptVersion.version}/publish`, {})
    const receiptTask = await post('/api/tasks', { workflowId: receiptWorkflow.id, workflowVersion: receiptVersion.version,
      name: '提交后故障', maxRetries: 0, items: [person()] }, operatorHeaders)
    const receiptApprovals = await (await request.get(`${api}/api/task-approvals`, { headers: admin })).json() as Array<{ id: string, taskId: string }>
    const receiptApproval = receiptApprovals.find(entry => entry.taskId === receiptTask.id)
    expect(receiptApproval).toBeDefined()
    await post(`/api/task-approvals/${receiptApproval!.id}/decide`, { approved: true })
    await post(`/api/tasks/${receiptTask.id}/queue`, {}, operatorHeaders)
    let receiptExecutionId: string | undefined
    for (let attempt = 0; attempt < 65; attempt++) {
      const detail = await (await request.get(`${api}/api/tasks/${receiptTask.id}`, { headers: operatorHeaders })).json() as
        { status: string, items: Array<{ executions: Array<{ id: string }> }> }
      if (detail.status === 'Failed') { receiptExecutionId = detail.items[0].executions[0].id; break }
      await new Promise(done => setTimeout(done, 1000))
    }
    expect(receiptExecutionId, `NodeAgent 日志:\n${output}`).toBeDefined()
    const evidenceResponse = await request.get(`${api}/api/task-reconciliations/${receiptExecutionId}/mock-evidence`, { headers: admin })
    expect(evidenceResponse.ok(), await evidenceResponse.text()).toBeTruthy()
    const proof = await evidenceResponse.json() as { result: string, evidenceReference: string }
    expect(proof.result).toBe('Submitted')
    expect(proof.evidenceReference).toMatch(/^mock-receipt:/)
    await page.getByRole('button', { name: '刷新' }).click()
    const receiptRow = page.locator('tr').filter({ hasText: receiptTask.id })
    await receiptRow.getByRole('button', { name: '查询 Mock 回执' }).click()
    await expect(receiptRow.getByRole('status')).toContainText('回执与当前参保状态一致')
    await expect(page.getByLabel(`外部核验凭据 ${receiptTask.id}`)).toHaveValue(proof.evidenceReference)
    await page.getByLabel(`核验结论 ${receiptTask.id}`).selectOption('Submitted')
    await receiptRow.getByRole('button', { name: '确认核验' }).click()
    await expect(page.getByRole('status')).toContainText('核验结论已记录')

    // Disabling a workflow stops an in-flight node before it can reach the external submit step.
    const stoppedDefinition = JSON.parse(definition) as { steps: Array<{ id: string, type: string, config: Record<string, unknown>, timeoutMs?: number }> }
    stoppedDefinition.steps.unshift({ id: 'pause-before-submit', type: 'Wait', config: { milliseconds: 20_000 }, timeoutMs: 25_000 })
    const stoppedWorkflow = await post('/api/workflows', { businessFunctionId: businessFunction.id, name: '停用运行中流程', description: 'NodeAgent 取消验收' })
    const stoppedVersion = await post(`/api/workflows/${stoppedWorkflow.id}/versions`, { definitionJson: JSON.stringify(stoppedDefinition) })
    await post(`/api/workflows/${stoppedWorkflow.id}/versions/${stoppedVersion.version}/publish`, {})
    const stoppedTask = await post('/api/tasks', { workflowId: stoppedWorkflow.id, workflowVersion: stoppedVersion.version,
      name: '运行中停用', maxRetries: 0, items: [JSON.stringify({ mockBaseUrl: api, employeeName: '未提交测试', idNumber: '110105194912310046', submissionId: randomUUID() })] }, operatorHeaders)
    const stoppedApprovals = await (await request.get(`${api}/api/task-approvals`, { headers: admin })).json() as Array<{ id: string, taskId: string }>
    const stoppedApproval = stoppedApprovals.find(entry => entry.taskId === stoppedTask.id)
    expect(stoppedApproval).toBeDefined()
    await post(`/api/task-approvals/${stoppedApproval!.id}/decide`, { approved: true })
    await post(`/api/tasks/${stoppedTask.id}/queue`, {}, operatorHeaders)
    let stoppedExecutionId: string | undefined
    let startedWait = false
    for (let attempt = 0; attempt < 55; attempt++) {
      const detail = await (await request.get(`${api}/api/tasks/${stoppedTask.id}`, { headers: operatorHeaders })).json() as
        { items: Array<{ executions: Array<{ id: string }> }> }
      stoppedExecutionId = detail.items[0].executions[0]?.id
      if (stoppedExecutionId) {
        const checkpoints = await (await request.get(`${api}/api/executions/${stoppedExecutionId}/checkpoints`, { headers: operatorHeaders })).json() as Array<{ stepId: string, eventType: string }>
        if (checkpoints.some(x => x.stepId === 'pause-before-submit' && x.eventType === 'StepStarted')) { startedWait = true; break }
      }
      await new Promise(done => setTimeout(done, 1000))
    }
    expect(stoppedExecutionId).toBeDefined()
    expect(startedWait, `NodeAgent 日志:\n${output}`).toBe(true)
    const disabledAt = Date.now()
    const disableResult = await post(`/api/workflows/${stoppedWorkflow.id}/disable`, {}) as { activeExecutions: number, signaled: number }
    expect(disableResult.activeExecutions).toBe(1)
    expect(disableResult.signaled).toBe(1)
    let stoppedStatus: string | undefined
    for (let attempt = 0; attempt < 25; attempt++) {
      const detail = await (await request.get(`${api}/api/tasks/${stoppedTask.id}`, { headers: operatorHeaders })).json() as { status: string }
      stoppedStatus = detail.status
      if (stoppedStatus === 'Failed') break
      await new Promise(done => setTimeout(done, 1000))
    }
    expect(stoppedStatus, `NodeAgent 日志:\n${output}`).toBe('Failed')
    const stoppedDetail = await (await request.get(`${api}/api/tasks/${stoppedTask.id}`, { headers: operatorHeaders })).json() as
      { items: Array<{ executions: Array<{ status: string }> }> }
    expect(stoppedDetail.items[0].executions[0].status).toBe('Failed')
    expect(Date.now() - disabledAt, '取消命令必须打断 20 秒的等待步骤').toBeLessThan(15_000)
    const stopCheckpoints = await (await request.get(`${api}/api/executions/${stoppedExecutionId}/checkpoints`, { headers: operatorHeaders })).json() as Array<{ stepId: string, eventType: string }>
    expect(stopCheckpoints.some(x => x.stepId === 'submit')).toBe(false)
    expect(await (await request.get(`${api}/mock/qd-social-security/employees/status?idNumber=110105194912310046`)).json()).toMatchObject({ active: false })

    // An already waiting human handoff must not be resumed after the workflow is disabled.
    const handoffDefinition = JSON.parse(definition) as { steps: Array<{ id: string, type: string, config: Record<string, unknown> }> }
    handoffDefinition.steps.unshift({ id: 'handoff', type: 'HumanTask', config: {} })
    const handoffWorkflow = await post('/api/workflows', { businessFunctionId: businessFunction.id, name: '停用人工等待流程', description: '人工恢复门禁验收' })
    const handoffVersion = await post(`/api/workflows/${handoffWorkflow.id}/versions`, { definitionJson: JSON.stringify(handoffDefinition) })
    await post(`/api/workflows/${handoffWorkflow.id}/versions/${handoffVersion.version}/publish`, {})
    const handoffTask = await post('/api/tasks', { workflowId: handoffWorkflow.id, workflowVersion: handoffVersion.version,
      name: '等待人工接管时停用', maxRetries: 0, items: [person()] }, operatorHeaders)
    const handoffApprovals = await (await request.get(`${api}/api/task-approvals`, { headers: admin })).json() as Array<{ id: string, taskId: string }>
    const handoffApproval = handoffApprovals.find(entry => entry.taskId === handoffTask.id)
    expect(handoffApproval).toBeDefined()
    await post(`/api/task-approvals/${handoffApproval!.id}/decide`, { approved: true })
    await post(`/api/tasks/${handoffTask.id}/queue`, {}, operatorHeaders)
    let handoffExecutionId: string | undefined
    for (let attempt = 0; attempt < 35; attempt++) {
      const detail = await (await request.get(`${api}/api/tasks/${handoffTask.id}`, { headers: operatorHeaders })).json() as
        { status: string, items: Array<{ executions: Array<{ id: string, status: string }> }> }
      if (detail.items[0].executions[0]?.status === 'WaitingForHuman') {
        handoffExecutionId = detail.items[0].executions[0].id
        break
      }
      await new Promise(done => setTimeout(done, 1000))
    }
    expect(handoffExecutionId, `NodeAgent 日志:\n${output}`).toBeDefined()
    const handoff = await post('/api/human-interventions', { executionId: handoffExecutionId, type: 'QrLogin',
      title: '等待人工确认', expiresAt: new Date(Date.now() + 180_000).toISOString() }, operatorHeaders) as { id: string, qrToken: string }
    expect(handoff.qrToken).toBeTruthy()
    await post(`/api/workflows/${handoffWorkflow.id}/disable`, {})
    const consumed = await post(`/api/human-interventions/${handoff.id}/qr/consume`, { token: handoff.qrToken }, operatorHeaders) as { status: string }
    expect(consumed.status).toBe('Completed')
    const reused = await request.post(`${api}/api/human-interventions/${handoff.id}/qr/consume`, {
      headers: operatorHeaders, data: { token: handoff.qrToken }
    })
    expect(reused.status()).toBe(400)
    let handoffStatus: string | undefined
    for (let attempt = 0; attempt < 25; attempt++) {
      const detail = await (await request.get(`${api}/api/tasks/${handoffTask.id}`, { headers: operatorHeaders })).json() as { status: string }
      handoffStatus = detail.status
      if (handoffStatus === 'Failed') break
      await new Promise(done => setTimeout(done, 1000))
    }
    expect(handoffStatus, `NodeAgent 日志:\n${output}`).toBe('Failed')
    const handoffCheckpoints = await (await request.get(`${api}/api/executions/${handoffExecutionId}/checkpoints`, { headers: operatorHeaders })).json() as Array<{ stepId: string }>
    expect(handoffCheckpoints.some(x => x.stepId === 'submit')).toBe(false)

    // A broken navigation fails before any form is submitted. After checking that this person
    // is absent, the reviewer permits one explicit retry; the same broken definition fails again.
    const brokenDefinition = JSON.parse(definition) as { steps: Array<{ id: string, config: { url?: string } }> }
    brokenDefinition.steps.find(step => step.id === 'open')!.config.url = 'mock://missing'
    const brokenWorkflow = await post('/api/workflows', { businessFunctionId: businessFunction.id, name: '提交前失败演示', description: '核验未提交后重试' })
    const brokenVersion = await post(`/api/workflows/${brokenWorkflow.id}/versions`, { definitionJson: JSON.stringify(brokenDefinition) })
    await post(`/api/workflows/${brokenWorkflow.id}/versions/${brokenVersion.version}/publish`, {})
    const brokenTask = await post('/api/tasks', { workflowId: brokenWorkflow.id, workflowVersion: brokenVersion.version,
      name: '提交前故障', maxRetries: 1, items: [JSON.stringify({ mockBaseUrl: api, employeeName: '未提交测试', idNumber: '110105194912310046', submissionId: randomUUID() })] }, operatorHeaders)
    const brokenApprovals = await (await request.get(`${api}/api/task-approvals`, { headers: admin })).json() as Array<{ id: string, taskId: string }>
    const brokenApproval = brokenApprovals.find(item => item.taskId === brokenTask.id)
    expect(brokenApproval).toBeDefined()
    await post(`/api/task-approvals/${brokenApproval!.id}/decide`, { approved: true })
    await post(`/api/tasks/${brokenTask.id}/queue`, {}, operatorHeaders)
    const waitForFailure = async (retryCount: number, previousExecutionId?: string) => {
      let detail: { status: string, items: Array<{ retryCount: number, executions: Array<{ id: string, status: string }> }> } | undefined
      for (let attempt = 0; attempt < 35; attempt++) {
        detail = await (await request.get(`${api}/api/tasks/${brokenTask.id}`, { headers: operatorHeaders })).json()
        if (detail?.status === 'Failed' && detail.items[0].retryCount === retryCount &&
            detail.items[0].executions.some(execution => execution.status === 'Failed' && execution.id !== previousExecutionId)) break
        await new Promise(done => setTimeout(done, 1000))
      }
      expect(detail?.status, `NodeAgent 日志:\n${output}`).toBe('Failed')
      expect(detail!.items[0].retryCount).toBe(retryCount)
      const failedExecution = detail!.items[0].executions.find(execution => execution.status === 'Failed' && execution.id !== previousExecutionId)
      expect(failedExecution).toBeDefined()
      return failedExecution!.id
    }
    const firstFailureId = await waitForFailure(0)
    const absent = await (await request.get(`${api}/mock/qd-social-security/employees/status?idNumber=110105194912310046`)).json() as { active: boolean }
    expect(absent.active).toBe(false)
    const retryDecision = await post(`/api/task-reconciliations/${firstFailureId}/decide`, { decision: 'NotSubmitted', evidenceReference: 'MOCK-STATUS-ABSENT' })
    expect(retryDecision.itemStatus).toBe('Pending')
    const secondFailureId = await waitForFailure(1, firstFailureId)
    expect(secondFailureId).not.toBe(firstFailureId)
    const history = await (await request.get(`${api}/api/tasks/${brokenTask.id}`, { headers: operatorHeaders })).json() as { items: Array<{ executions: Array<{ id: string }> }> }
    expect(history.items[0].executions[0].id).toBe(secondFailureId)
    const exhausted = await request.post(`${api}/api/task-reconciliations/${secondFailureId}/decide`, {
      headers: admin, data: { decision: 'NotSubmitted', evidenceReference: 'MOCK-STATUS-ABSENT' }
    })
    expect(exhausted.status()).toBe(409)

    // Simulate a hard node crash with a lease past its deadline. The monitor must retain the
    // uncertain external outcome for manual review instead of automatically dispatching again.
    const crashDefinition = JSON.parse(definition) as { steps: Array<{ id: string, type: string, config: Record<string, unknown>, timeoutMs?: number }> }
    crashDefinition.steps.unshift({ id: 'crash-wait', type: 'Wait', config: { milliseconds: 120_000 }, timeoutMs: 120_000 })
    const crashWorkflow = await post('/api/workflows', { businessFunctionId: businessFunction.id, name: '节点异常退出', description: '租约到期后核验' })
    const crashVersion = await post(`/api/workflows/${crashWorkflow.id}/versions`, { definitionJson: JSON.stringify(crashDefinition) })
    await post(`/api/workflows/${crashWorkflow.id}/versions/${crashVersion.version}/publish`, {})
    const crashTask = await post('/api/tasks', { workflowId: crashWorkflow.id, workflowVersion: crashVersion.version,
      name: '崩溃后等待核验', maxRetries: 2, items: [person()] }, operatorHeaders)
    const crashApprovals = await (await request.get(`${api}/api/task-approvals`, { headers: admin })).json() as Array<{ id: string, taskId: string }>
    const crashApproval = crashApprovals.find(entry => entry.taskId === crashTask.id)
    expect(crashApproval).toBeDefined()
    await post(`/api/task-approvals/${crashApproval!.id}/decide`, { approved: true })
    await post(`/api/tasks/${crashTask.id}/queue`, {}, operatorHeaders)
    let crashExecutionId: string | undefined
    for (let attempt = 0; attempt < 35; attempt++) {
      const detail = await (await request.get(`${api}/api/tasks/${crashTask.id}`, { headers: operatorHeaders })).json() as
        { items: Array<{ executions: Array<{ id: string }> }> }
      const id = detail.items[0].executions[0]?.id
      if (id) {
        const checkpoints = await (await request.get(`${api}/api/executions/${id}/checkpoints`, { headers: operatorHeaders })).json() as Array<{ stepId: string, eventType: string }>
        if (checkpoints.some(x => x.stepId === 'crash-wait' && x.eventType === 'StepStarted')) { crashExecutionId = id; break }
      }
      await new Promise(done => setTimeout(done, 1000))
    }
    expect(crashExecutionId, `NodeAgent 日志:\n${output}`).toBeDefined()
    child.kill('SIGKILL')
    await new Promise<void>(resolve => child.once('exit', () => resolve()))
    const dbResult = spawnSync('python3', ['-c', `import sqlite3,sys
db=sqlite3.connect(sys.argv[2],timeout=10)
assert db.execute("UPDATE execution_nodes SET Status='Offline' WHERE lower(Id)=lower(?)",(sys.argv[1],)).rowcount == 1
assert db.execute("UPDATE NodeLeases SET ExpiresAt='2000-01-01 00:00:00+00:00' WHERE Released=0").rowcount >= 1
db.commit()
`, node.nodeId, resolve(process.cwd(), '../backend/AgentRPA.Api/browser-test.db')], { cwd: process.cwd(), encoding: 'utf8' })
    expect(dbResult.status, dbResult.stderr).toBe(0)
    let crashDetail: { status: string, items: Array<{ retryCount: number, executions: Array<{ id: string, status: string }> }> } | undefined
    for (let attempt = 0; attempt < 35; attempt++) {
      crashDetail = await (await request.get(`${api}/api/tasks/${crashTask.id}`, { headers: operatorHeaders })).json()
      if (crashDetail?.status === 'Failed') break
      await new Promise(done => setTimeout(done, 1000))
    }
    expect(crashDetail?.status).toBe('Failed')
    expect(crashDetail?.items[0].retryCount).toBe(0)
    expect(crashDetail?.items[0].executions).toHaveLength(1)
    expect(crashDetail?.items[0].executions[0]).toMatchObject({ id: crashExecutionId, status: 'Failed' })
    const crashPending = await (await request.get(`${api}/api/task-reconciliations`, { headers: admin })).json() as Array<{ executionId: string }>
    expect(crashPending.some(x => x.executionId === crashExecutionId)).toBe(true)
  } finally {
    child.kill('SIGTERM')
  }
})

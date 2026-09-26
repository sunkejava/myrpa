import { expect, test } from '@playwright/test'

const api = 'http://127.0.0.1:5000'
const password = 'BrowserTestPassword123!'

test('login, resource setup, natural language planning and task submission', async ({ page, request, isMobile }) => {
  test.skip(isMobile, 'This workflow seeds one shared city; mobile layout is verified independently.')
  test.setTimeout(90_000)
  await page.goto('/')
  await expect(page.getByRole('heading', { name: '登录 AgentRPA' })).toBeVisible()
  await page.getByLabel('用户名').fill('admin')
  await page.getByLabel('密码').fill(password)
  await page.getByRole('button', { name: '登录', exact: true }).click()
  await expect(page.getByRole('heading', { name: '今天需要帮你处理什么？' })).toBeVisible()
  await page.getByRole('button', { name: 'LLM 用量' }).click()
  await expect(page.getByRole('heading', { name: '我的 LLM 用量' })).toBeVisible()
  await expect(page.getByText('Token · 总计')).toBeVisible()
  await page.getByRole('button', { name: '人工介入' }).click()
  await expect(page.getByRole('button', { name: '创建人工介入' })).toBeVisible()
  await page.getByRole('button', { name: '审计记录' }).click()
  await expect(page.getByRole('heading', { name: '审计记录', level: 2 })).toBeVisible()
  await page.getByRole('button', { name: 'AI 工作台' }).click()
  const token = (await (await request.post(`${api}/api/auth/login`, {
    data: { userName: 'admin', password }
  })).json()).accessToken as string
  const headers = { Authorization: `Bearer ${token}` }
  const city = await (await request.post(`${api}/api/business-resources/cities`, {
    headers, data: { code: 'CN-SD-QD', name: '青岛市' }
  })).json() as { id: string }
  const system = await (await request.post(`${api}/api/business-resources/cities/${city.id}/systems`, {
    headers, data: { code: 'SOCIAL', name: '社保系统' }
  })).json() as { id: string }
  const businessFunction = await (await request.post(`${api}/api/business-resources/systems/${system.id}/functions`, {
    headers, data: { code: 'EMPLOYEE.ADD', name: '增员' }
  })).json() as { id: string }
  const workflow = await (await request.post(`${api}/api/workflows`, {
    headers, data: { businessFunctionId: businessFunction.id, name: '青岛增员演示', description: '浏览器集成测试' }
  })).json() as { id: string }
  const version = await (await request.post(`${api}/api/workflows/${workflow.id}/versions`, {
    headers, data: { definitionJson: '{"steps":[{"type":"End"}]}' }
  })).json() as { version: number }
  expect((await request.post(`${api}/api/workflows/${workflow.id}/versions/${version.version}/publish`, { headers, data: {} })).ok()).toBeTruthy()
  expect((await request.post(`${api}/api/permission-policies`, {
    headers, data: { subjectId: (await (await request.get(`${api}/api/auth/me`, { headers })).json()).userId,
      cityId: city.id, systemId: system.id, functionId: businessFunction.id, action: 'Execute' }
  })).ok()).toBeTruthy()

  await page.getByRole('button', { name: '城市与系统' }).click()
  await expect(page.getByRole('option', { name: /青岛市/ })).toBeAttached()
  await page.getByLabel('资源类型').selectOption('country')
  await page.getByLabel('编码').fill('CN')
  await page.getByLabel('名称').fill('中国')
  await page.getByRole('button', { name: '创建', exact: true }).click()
  await page.locator('.resource-grid select').first().selectOption({ label: '中国 (CN)' })
  await page.getByLabel('资源类型').selectOption('province')
  await page.getByLabel('编码').fill('SD')
  await page.getByLabel('名称').fill('山东省')
  await page.getByRole('button', { name: '创建', exact: true }).click()
  await expect(page.getByRole('option', { name: /山东省/ })).toBeAttached()
  const role = await (await request.post(`${api}/api/user-management/roles`, {
    headers, data: { name: 'Runner', displayName: '执行员' }
  })).json() as { id: string }
  const uiUserName = `ui-runner-${Date.now()}`
  await page.getByRole('button', { name: '账户与角色' }).click()
  await page.getByLabel('登录名').fill(uiUserName)
  await page.getByLabel('显示名称', { exact: true }).first().fill('页面执行员')
  await page.getByLabel('初始密码').fill('PageRunnerPassword123!')
  await page.getByRole('button', { name: '创建账户' }).click()
  await expect(page.getByRole('status')).toContainText('账户已创建')
  await page.getByRole('combobox', { name: '选择账户' }).selectOption({ label: `页面执行员 (${uiUserName})` })
  await page.getByRole('combobox', { name: '选择角色' }).selectOption(role.id)
  await page.getByRole('button', { name: '分配角色' }).click()
  await expect(page.getByText('已分配：执行员')).toBeVisible()
  await page.getByRole('button', { name: '权限中心' }).click()
  await page.getByLabel('授权对象').selectOption('role')
  await page.locator('form select').nth(1).selectOption(role.id)
  await page.getByLabel('城市').selectOption(city.id)
  await page.locator('form .resource-grid select').nth(1).selectOption(system.id)
  await page.getByLabel('功能').selectOption(businessFunction.id)
  await page.getByRole('button', { name: '显式拒绝' }).click()
  await expect(page.getByText('角色 · 执行员')).toBeVisible()
  await page.getByRole('button', { name: 'AI 工作台' }).click()
  await page.getByLabel('任务描述').fill('青岛市 社保系统 增员')
  await page.getByRole('button', { name: '生成计划' }).click()
  await expect(page.getByRole('alert')).toHaveCount(0)
  await expect(page.getByRole('heading', { name: '规划结果' })).toBeVisible()
  await expect(page.getByText('青岛增员演示', { exact: false })).toBeVisible()
  await page.getByRole('button', { name: '提交任务' }).click()
  await expect(page.getByRole('heading', { name: '任务中心' })).toBeVisible()
  await expect(page.getByText('Agent: Execute')).toBeVisible()
  await page.getByRole('button', { name: '查看详情' }).first().click()
  await expect(page.getByText('任务详情', { exact: false })).toBeVisible()
  const riskyWorkflow = await (await request.post(`${api}/api/workflows`, {
    headers, data: { businessFunctionId: businessFunction.id, name: '审批演示', description: '高风险审批' }
  })).json() as { id: string }
  const riskyVersion = await (await request.post(`${api}/api/workflows/${riskyWorkflow.id}/versions`, {
    headers, data: { definitionJson: '{"riskLevel":"High","steps":[{"type":"End"}]}' }
  })).json() as { version: number }
  expect((await request.post(`${api}/api/workflows/${riskyWorkflow.id}/versions/${riskyVersion.version}/publish`, { headers, data: {} })).ok()).toBeTruthy()
  const riskyTask = await (await request.post(`${api}/api/tasks`, {
    headers, data: { workflowId: riskyWorkflow.id, workflowVersion: riskyVersion.version, name: '待审核任务', items: ['{}'] }
  })).json() as { id: string; approvalRequired: boolean }
  expect(riskyTask.approvalRequired).toBeTruthy()
  await page.getByRole('button', { name: '审批中心' }).click()
  await expect(page.getByText(riskyTask.id)).toBeVisible()
  await page.locator('tr').filter({ hasText: riskyTask.id }).getByRole('button', { name: '批准' }).click()
  await expect(page.getByRole('alert')).toContainText('不得审批本人')
  await page.getByRole('button', { name: 'Workflow 管理' }).click()
  await page.locator('.resource-grid select').first().selectOption(city.id)
  await page.locator('.resource-grid select').nth(1).selectOption(system.id)
  await page.getByLabel('功能').selectOption(businessFunction.id)
  await page.getByLabel('名称').fill('可发布流程')
  await page.getByRole('button', { name: '创建 Workflow' }).click()
  await page.getByRole('combobox', { name: 'Workflow 风险级别' }).selectOption('High')
  await page.getByRole('button', { name: '创建并发布新版本' }).click()
  await expect(page.getByRole('status')).toContainText('已发布')
})

test('theme switch and mobile layout', async ({ page, isMobile }) => {
  await page.goto('/')
  await page.getByRole('button', { name: '切换主题' }).click()
  await expect(page.locator('main.app-shell')).toHaveClass(/theme-light/)
  await page.reload()
  await expect(page.locator('main.app-shell')).toHaveClass(/theme-light/)
  if (isMobile) {
    const [contentWidth, viewportWidth] = await page.evaluate(() => [document.documentElement.scrollWidth, window.innerWidth])
    expect(contentWidth).toBeLessThanOrEqual(viewportWidth + 1)
  }
})

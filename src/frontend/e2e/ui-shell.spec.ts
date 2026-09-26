import { expect, test } from '@playwright/test'
import { randomUUID } from 'node:crypto'

test('登录校验、会话刷新、主题语言及全部菜单入口', async ({ page, request }) => {
  test.setTimeout(120_000)
  await page.goto('/')
  await page.getByLabel('用户名').fill('admin')
  await page.getByLabel('密码').fill('wrong-password')
  await page.getByRole('button', { name: '登录', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('用户名或密码错误')
  await page.getByLabel('密码').fill('BrowserTestPassword123!')
  await page.getByRole('button', { name: '登录', exact: true }).click()
  await expect(page.getByRole('heading', { name: '今天需要帮你处理什么？' })).toBeVisible()
  await page.reload()
  await expect(page.getByRole('heading', { name: '今天需要帮你处理什么？' })).toBeVisible()

  const pages = ['任务中心', '城市与系统', '人工介入', 'LLM 用量', '权限中心', '审批中心',
    '核验中心', 'Workflow 管理', '节点管理', '节点池', '节点能力', '调度监控', '审计记录', '账户与角色']
  for (const label of pages) {
    await page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: label }).click()
    await expect(page.getByRole('heading', { level: 1, name: label })).toBeVisible()
    await expect(page.getByRole('alert')).toHaveCount(0)
  }
  await page.getByRole('button', { name: '主题设置' }).click()
  await page.getByRole('combobox', { name: '语言', exact: true }).selectOption('en')
  await expect(page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Tasks' })).toBeVisible()
  await page.getByRole('combobox', { name: 'Language', exact: true }).selectOption('zh')
  await page.locator('.theme-settings input[type="file"]').setInputFiles({ name: 'custom.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify({ system: { tasks: 'Custom tasks' } })) })
  await expect(page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: 'Custom tasks' })).toBeVisible()
  await page.getByRole('combobox', { name: '语言', exact: true }).selectOption('zh')
  await page.getByLabel('主色').fill('#d05590')
  await page.getByLabel('明暗模式').selectOption('light')
  await page.reload()
  await expect(page.locator('.app-shell')).toHaveClass(/theme-light/)
  await expect(page.locator('.app-shell')).toHaveCSS('--accent', '#d05590')
  await page.getByRole('button', { name: '退出登录' }).click()
  await expect(page.getByRole('heading', { name: '登录 AgentRPA' })).toBeVisible()

  // 无效缓存会话必须通过 /auth/me 拒绝，不能依靠本地 admin 标识绕过校验。
  await page.evaluate(() => { sessionStorage.setItem('agentrpa-token', 'invalid'); sessionStorage.setItem('agentrpa-admin', 'true') })
  await page.reload()
  await expect(page.getByRole('heading', { name: '登录 AgentRPA' })).toBeVisible()
  expect(await page.evaluate(() => sessionStorage.getItem('agentrpa-token'))).toBeNull()
  const response = await request.post('http://127.0.0.1:5000/api/auth/login', {
    data: { userName: 'admin', password: 'BrowserTestPassword123!' }
  })
  expect(response.ok()).toBeTruthy()
})

test('普通账户只能进入自己的功能页面', async ({ page, request }) => {
  const adminLogin = await request.post('http://127.0.0.1:5000/api/auth/login', {
    data: { userName: 'admin', password: 'BrowserTestPassword123!' }
  })
  expect(adminLogin.ok()).toBeTruthy()
  const adminToken = (await adminLogin.json() as { accessToken: string }).accessToken
  const userName = `ui-${randomUUID().slice(0, 12)}`
  const created = await request.post('http://127.0.0.1:5000/api/user-management/users', {
    headers: { Authorization: `Bearer ${adminToken}` },
    data: { userName, displayName: '普通测试用户', password: 'UiTestPassword123!' }
  })
  expect(created.ok()).toBeTruthy()
  await page.goto('/')
  await page.getByLabel('用户名').fill(userName)
  await page.getByLabel('密码').fill('UiTestPassword123!')
  await page.getByRole('button', { name: '登录', exact: true }).click()
  await expect(page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: '任务中心' })).toBeVisible()
  await expect(page.getByRole('navigation', { name: 'Main navigation' }).getByRole('button', { name: '账户与角色' })).toHaveCount(0)
  const token = await page.evaluate(() => sessionStorage.getItem('agentrpa-token'))
  expect((await request.get('http://127.0.0.1:5000/api/dispatch-monitor', {
    headers: { Authorization: `Bearer ${token}` }
  })).status()).toBe(403)
  await page.getByRole('button', { name: '任务中心' }).click()
  await expect(page.getByRole('heading', { level: 1, name: '任务中心' })).toBeVisible()
})

test('角色分配、撤销、停用及管理员保护', async ({ request }) => {
  const login = await request.post('http://127.0.0.1:5000/api/auth/login', { data: { userName: 'admin', password: 'BrowserTestPassword123!' } })
  expect(login.ok()).toBeTruthy()
  const headers = { Authorization: `Bearer ${(await login.json() as { accessToken: string }).accessToken}` }
  const root = 'http://127.0.0.1:5000/api/user-management'
  const name = `role-${randomUUID().slice(0, 12)}`
  const role = await request.post(`${root}/roles`, { headers, data: { name, displayName: '验收角色' } })
  expect(role.status()).toBe(201)
  const roleId = (await role.json() as { id: string }).id
  const memberName = `member-${randomUUID().slice(0, 12)}`
  const user = await request.post(`${root}/users`, { headers, data: { userName: memberName, displayName: '验收成员', password: 'AcceptancePassword123!' } })
  expect(user.status()).toBe(201)
  const userId = (await user.json() as { id: string }).id
  const memberLogin = await request.post('http://127.0.0.1:5000/api/auth/login', { data: { userName: memberName, password: 'AcceptancePassword123!' } })
  expect(memberLogin.ok()).toBeTruthy()
  const memberHeaders = { Authorization: `Bearer ${(await memberLogin.json() as { accessToken: string }).accessToken}` }
  expect((await request.post(`${root}/users/${userId}/roles/${roleId}`, { headers })).ok()).toBeTruthy()
  expect((await request.get('http://127.0.0.1:5000/api/auth/me', { headers: memberHeaders })).status()).toBe(401)
  const assignedLogin = await request.post('http://127.0.0.1:5000/api/auth/login', { data: { userName: memberName, password: 'AcceptancePassword123!' } })
  const assignedHeaders = { Authorization: `Bearer ${(await assignedLogin.json() as { accessToken: string }).accessToken}` }
  expect((await request.get(`${root}/users/${userId}/roles`, { headers }).then(r => r.json()) as Array<{ id: string }>).some(r => r.id === roleId)).toBeTruthy()
  expect((await request.delete(`${root}/users/${userId}/roles/${roleId}`, { headers })).status()).toBe(204)
  expect((await request.get('http://127.0.0.1:5000/api/auth/me', { headers: assignedHeaders })).status()).toBe(401)
  expect((await request.get(`${root}/users/${userId}/roles`, { headers }).then(r => r.json()) as Array<{ id: string }>).some(r => r.id === roleId)).toBeFalsy()
  expect((await request.post(`${root}/roles/${roleId}/enabled`, { headers, data: { enabled: false } })).ok()).toBeTruthy()
  expect((await request.post(`${root}/users/${userId}/roles/${roleId}`, { headers })).status()).toBe(404)
  const adminRole = (await request.get(`${root}/roles`, { headers }).then(r => r.json()) as Array<{ id: string; name: string }>).find(r => r.name === 'Admin')!
  const me = await request.get('http://127.0.0.1:5000/api/auth/me', { headers }).then(r => r.json()) as { userId: string }
  expect((await request.post(`${root}/roles/${adminRole.id}/enabled`, { headers, data: { enabled: false } })).status()).toBe(409)
  expect((await request.delete(`${root}/users/${me.userId}/roles/${adminRole.id}`, { headers })).status()).toBe(409)
})

test('资源页可以查看并切换业务系统状态', async ({ page, request }) => {
  const login = await request.post('http://127.0.0.1:5000/api/auth/login', { data: { userName: 'admin', password: 'BrowserTestPassword123!' } })
  const headers = { Authorization: `Bearer ${(await login.json() as { accessToken: string }).accessToken}` }
  const root = 'http://127.0.0.1:5000/api/business-resources'
  const key = randomUUID().slice(0, 8)
  const cityResponse = await request.post(`${root}/cities`, { headers, data: { code: `UI-${key}`, name: `验收城市-${key}` } })
  expect(cityResponse.status()).toBe(201)
  const cityId = (await cityResponse.json() as { id: string }).id
  const systemResponse = await request.post(`${root}/cities/${cityId}/systems`, { headers, data: { code: `SYS-${key}`, name: `验收系统-${key}` } })
  expect(systemResponse.status()).toBe(201)
  await page.goto('/')
  await page.getByLabel('用户名').fill('admin')
  await page.getByLabel('密码').fill('BrowserTestPassword123!')
  await page.getByRole('button', { name: '登录', exact: true }).click()
  await page.getByRole('button', { name: '城市与系统' }).click()
  await page.getByLabel('城市').selectOption(cityId)
  const systemRow = page.getByRole('row').filter({ hasText: `验收系统-${key}` })
  await expect(systemRow).toContainText('启用')
  await systemRow.getByRole('button', { name: '停用' }).click()
  await expect(systemRow).toContainText('停用')
  await systemRow.getByRole('button', { name: '启用' }).click()
  await expect(systemRow).toContainText('启用')
})

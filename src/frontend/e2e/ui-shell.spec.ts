import { expect, test } from '@playwright/test'

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

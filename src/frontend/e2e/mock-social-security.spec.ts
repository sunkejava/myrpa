import { expect, test } from '@playwright/test'

const site = 'http://127.0.0.1:5000/mock/qd-social-security'
const employee = { employeeName: '测试人员', idNumber: '11010519491231002X' }

test('青岛模拟社保登录、增员、重复申报、减员及校验', async ({ page, request, isMobile }) => {
  test.skip(isMobile, 'Mock 服务共用人员数据，桌面浏览器覆盖完整业务链。')
  expect((await request.get(`${site}/employees/status?idNumber=${employee.idNumber}`)).status()).toBe(401)
  await page.goto(`${site}/employees`)
  await expect(page.getByRole('heading', { name: '登录' })).toBeVisible()
  await page.locator('[name=username]').fill('demo')
  await page.locator('[name=password]').fill('Demo123!')
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page.getByRole('heading', { name: '青岛社保增减员' })).toBeVisible()

  const submit = async (operation: string, idNumber = employee.idNumber, submissionId = '') => {
    await page.locator('[name=operation]').selectOption(operation)
    await page.locator('[name=employeeName]').fill(employee.employeeName)
    await page.locator('[name=idNumber]').fill(idNumber)
    await page.locator('[name=submissionId]').fill(submissionId)
    await page.getByRole('button', { name: '提交申报' }).click()
  }
  await submit('add', '110105194912310021')
  await expect(page.locator('[data-result=error]')).toContainText('无效')
  await page.getByRole('link', { name: '返回' }).click()
  await submit('add', employee.idNumber, 'mock-add-replay-001')
  await expect(page.locator('[data-result=success]')).toContainText('增员申报成功')
  await page.getByRole('link', { name: '返回' }).click()
  await submit('add', employee.idNumber, 'mock-add-replay-001')
  await expect(page.locator('[data-result=success]')).toContainText('增员申报成功')
  await page.getByRole('link', { name: '返回' }).click()
  await submit('add')
  await expect(page.locator('[data-result=error]')).toContainText('已参保')
  await page.getByRole('link', { name: '返回' }).click()
  await submit('remove')
  await expect(page.locator('[data-result=success]')).toContainText('减员申报成功')
  await page.getByRole('link', { name: '返回' }).click()
  await submit('remove')
  await expect(page.locator('[data-result=error]')).toContainText('不存在')
})

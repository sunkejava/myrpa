import mockAdd from './qd-social-security-add.json'
import mockRemove from './qd-social-security-remove.json'

export type WorkflowTemplate = { code: string; name: string; summary: string; resource: string; requirements: string; definition: Record<string, unknown>; developmentOnly?: boolean }

const personQuery = {
  riskLevel: 'High', requiresApproval: true,
  parameters: { personId: { type: 'string', required: true, sensitive: true, maxLength: 64 } },
  steps: [
    { id: 'open', type: 'Navigate', config: { url: '{{systemBaseUrl}}' } },
    { id: 'login', type: 'HumanTask' },
    { id: 'person-id', type: 'Input', config: { selector: '[data-testid=replace-person-id]', value: '{{personId}}' } },
    { id: 'query', type: 'Click', config: { selector: '[data-testid=replace-query]' } },
    { id: 'result', type: 'WaitForElement', config: { selector: '[data-testid=replace-result]' } },
    { id: 'download', type: 'Download', config: { selector: '[data-testid=replace-export]', path: 'artifacts/result.xlsx' } },
    { id: 'end', type: 'End' }
  ]
}

const unitCertificate = {
  riskLevel: 'High', requiresApproval: true,
  parameters: { organizationCode: { type: 'string', required: true, sensitive: true, maxLength: 64 } },
  steps: [
    { id: 'open', type: 'Navigate', config: { url: '{{systemBaseUrl}}' } },
    { id: 'login', type: 'HumanTask' },
    { id: 'organization', type: 'Input', config: { selector: '[data-testid=replace-organization-code]', value: '{{organizationCode}}' } },
    { id: 'query', type: 'Click', config: { selector: '[data-testid=replace-query]' } },
    { id: 'download', type: 'Download', config: { selector: '[data-testid=replace-export]', path: 'artifacts/unit-certificate.pdf' } },
    { id: 'end', type: 'End' }
  ]
}

export const workflowTemplates: WorkflowTemplate[] = [
  { code: 'beijing-medical-query', name: '北京医保 · 人员信息查询', summary: '登录接管、按人员查询并下载结果。', resource: '北京 / 医保 / PERSON-QUERY', requirements: '配置系统地址；替换全部 replace-* 选择器；核实登录与下载权限。', definition: personQuery },
  { code: 'unit-certificate', name: '单位参保证明', summary: '按单位识别信息查询并下载证明。', resource: '所选城市 / 社保或医保 / UNIT-CERTIFICATE', requirements: '配置系统地址；替换全部 replace-* 选择器；确认实际平台支持下载。', definition: unitCertificate },
  { code: 'qd-mock-add', name: '青岛社保模拟 · 增员', summary: '对开发环境模拟站点提交增员并检查回执。', resource: '青岛 / 社保 / PERSON-ADD', requirements: '仅开发环境；NodeAgent 需具备 Adapter:qd-social-security；提交 mockBaseUrl 等参数。', definition: mockAdd, developmentOnly: true },
  { code: 'qd-mock-remove', name: '青岛社保模拟 · 减员', summary: '对开发环境模拟站点提交减员并检查回执。', resource: '青岛 / 社保 / PERSON-REMOVE', requirements: '仅开发环境；NodeAgent 需具备 Adapter:qd-social-security；提交 mockBaseUrl 等参数。', definition: mockRemove, developmentOnly: true }
]

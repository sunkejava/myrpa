import { ref } from 'vue'
import { ApiError } from '../api/http'
import { currentUser, type AuthSession } from '../api/modules/auth'

const token = ref(sessionStorage.getItem('agentrpa-token') || '')
const userName = ref(sessionStorage.getItem('agentrpa-user') || '')
const admin = ref(sessionStorage.getItem('agentrpa-admin') === 'true')
const checking = ref(!!token.value)

function setSession(session: AuthSession) {
  token.value = session.accessToken
  userName.value = session.userName
  admin.value = session.roles.includes('Admin')
  sessionStorage.setItem('agentrpa-token', token.value)
  sessionStorage.setItem('agentrpa-user', userName.value)
  sessionStorage.setItem('agentrpa-admin', String(admin.value))
}
function clearSession() {
  token.value = ''; userName.value = ''; admin.value = false
  sessionStorage.removeItem('agentrpa-token')
  sessionStorage.removeItem('agentrpa-user')
  sessionStorage.removeItem('agentrpa-admin')
}
async function validateSession() {
  checking.value = !!token.value
  if (!token.value) return
  try {
    const user = await currentUser(token.value)
    userName.value = user.userName
    admin.value = user.roles.includes('Admin')
    sessionStorage.setItem('agentrpa-user', userName.value)
    sessionStorage.setItem('agentrpa-admin', String(admin.value))
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) clearSession()
    else clearSession() // 无法验证的旧会话不应继续进入工作台
  } finally { checking.value = false }
}
export function useSession() { return { token, userName, admin, checking, setSession, clearSession, validateSession } }

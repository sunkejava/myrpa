<script setup lang="ts">
import { ref } from 'vue'

const emit = defineEmits<{ authenticated: [session: { accessToken: string; userName: string; roles: string[] }] }>()
const userName = ref('')
const password = ref('')
const error = ref('')
const busy = ref(false)

async function login() {
  busy.value = true
  error.value = ''
  try {
    const response = await fetch('/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userName: userName.value, password: password.value }) })
    if (!response.ok) {
      const body: unknown = await response.json().catch(() => null)
      throw new Error(body && typeof body === 'object' && 'message' in body ? String(body.message) : `登录失败 (${response.status})`)
    }
    emit('authenticated', await response.json() as { accessToken: string; userName: string; roles: string[] })
    password.value = ''
  } catch (e) { error.value = e instanceof Error ? e.message : '登录失败' }
  finally { busy.value = false }
}
</script>

<template>
  <p v-if="error" class="error" role="alert">{{ error }}</p>
  <section class="panel form-panel">
    <h2>登录 AgentRPA</h2>
    <form @submit.prevent="login">
      <label>用户名<input v-model.trim="userName" required autocomplete="username" /></label>
      <label>密码<input v-model="password" type="password" required autocomplete="current-password" /></label>
      <button class="action-btn primary" :disabled="busy">{{ busy ? '登录中…' : '登录' }}</button>
    </form>
  </section>
</template>

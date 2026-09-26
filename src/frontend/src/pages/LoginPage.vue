<script setup lang="ts">
import { ref } from 'vue'
import { login as authenticate, type AuthSession } from '../api/modules/auth'
import { useLocale } from '../locales'

const emit = defineEmits<{ authenticated: [session: AuthSession] }>()
const userName = ref('')
const password = ref('')
const error = ref('')
const busy = ref(false)
const { t } = useLocale()

async function login() {
  busy.value = true
  error.value = ''
  try {
    emit('authenticated', await authenticate(userName.value, password.value))
    password.value = ''
  } catch (e) { error.value = e instanceof Error ? e.message : '登录失败' }
  finally { busy.value = false }
}
</script>

<template>
  <p v-if="error" class="error" role="alert">{{ error }}</p>
  <section class="panel form-panel">
    <h2>{{ t('common.signInHeading') }}</h2>
    <form @submit.prevent="login">
      <label>{{ t('common.userName') }}<input v-model.trim="userName" required autocomplete="username" /></label>
      <label>{{ t('common.password') }}<input v-model="password" type="password" required autocomplete="current-password" /></label>
      <button class="action-btn primary" :disabled="busy">{{ busy ? t('common.signingIn') : t('common.login') }}</button>
    </form>
  </section>
</template>

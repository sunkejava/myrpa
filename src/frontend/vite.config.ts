import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: { '/api': { target: process.env.VITE_API_PROXY_TARGET || 'http://localhost:5000', changeOrigin: true } }
  },
  build: { target: 'es2022' }
})

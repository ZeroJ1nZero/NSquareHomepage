import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'https://localhost:7001',
        changeOrigin: true,
        secure: false
      },
      '/connect': {
        target: 'https://localhost:7213',
        changeOrigin: true,
        secure: false
      }
    }
  }
})

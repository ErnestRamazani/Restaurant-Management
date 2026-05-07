import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  base: '/menu/',
  build: {
    outDir: path.resolve(__dirname, '../EliteRestaurant.Api/wwwroot/menu'),
    emptyOutDir: true,
  },
  server: {
    host: true,
    port: 5173,
    // Browser opens to the app once the server is ready.
    open: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5223',
        changeOrigin: true,
      },
    },
  },
})
